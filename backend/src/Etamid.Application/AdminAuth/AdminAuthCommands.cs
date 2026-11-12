using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Admin;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.AdminAuth;

internal static class AdminCodes
{
    public static bool IsSixDigits(string? code) => code is { Length: 6 } && code.All(c => c is >= '0' and <= '9');
}

// ======================================================================= Login
public sealed record AdminLoginCommand(string Email, string Password) : IRequest<Result<AdminLoginResultDto>>;

public sealed class AdminLoginValidator : AbstractValidator<AdminLoginCommand>
{
    public AdminLoginValidator()
    {
        RuleFor(x => x.Email).MustBeRequired(190);
        RuleFor(x => x.Password).MustBeRequired(200);
    }
}

public sealed class AdminLoginHandler(
    IApplicationDbContext db, IPasswordHasher hasher, IClock clock,
    AdminSessionService sessions, AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<AdminLoginCommand, Result<AdminLoginResultDto>>
{
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<AdminLoginResultDto>> Handle(AdminLoginCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var now = clock.UtcNow;

        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == email, ct);
        if (admin is null)
        {
            // Same work and same answer as a wrong password.
            hasher.VerifyDummy(cmd.Password);
            audit.Record(AuditActorType.System, null, "admin.login.failed", "AdminUser", 0, null, new { email, reason = "unknown_email" });
            await db.SaveChangesAsync(ct);
            return AdminErrors.InvalidCredentials;
        }

        if (admin.IsLocked(now))
        {
            audit.Record(AuditActorType.Admin, admin.Id, "admin.login.blocked", "AdminUser", admin.Id, null, new { reason = "locked" });
            await db.SaveChangesAsync(ct);
            return AdminErrors.AccountLocked(admin.RemainingLockSeconds(now));
        }

        if (!hasher.Verify(admin.PasswordHash, cmd.Password))
        {
            var locked = admin.RecordFailedLogin(now);
            audit.Record(AuditActorType.Admin, admin.Id, "admin.login.failed", "AdminUser", admin.Id, null, new { reason = "wrong_password" });
            if (locked) audit.Record(AuditActorType.Admin, admin.Id, "admin.account.locked", "AdminUser", admin.Id);
            await db.SaveChangesAsync(ct);
            return locked ? AdminErrors.AccountLocked((int)AdminUser.LockoutDuration.TotalSeconds) : AdminErrors.InvalidCredentials;
        }

        if (admin.Status != AdminStatus.Active)
        {
            audit.Record(AuditActorType.Admin, admin.Id, "admin.login.failed", "AdminUser", admin.Id, null, new { reason = "disabled" });
            await db.SaveChangesAsync(ct);
            return AdminErrors.InvalidCredentials;
        }

        if (admin.TwoFactorEnabled)
        {
            var open = await db.AdminTwoFactorChallenges
                .Where(c => c.AdminUserId == admin.Id && c.ConsumedAt == null).ToListAsync(ct);
            foreach (var previous in open) previous.ConsumedAt = now;

            var raw = TokenHasher.NewToken();
            db.AdminTwoFactorChallenges.Add(new AdminTwoFactorChallenge
            {
                AdminUserId = admin.Id,
                TokenHash = TokenHasher.Hash(raw),
                ExpiresAt = now + ChallengeLifetime
            });
            audit.Record(AuditActorType.Admin, admin.Id, "admin.login.2fa_required", "AdminUser", admin.Id);
            await db.SaveChangesAsync(ct);
            return new AdminLoginResultDto(true, new AdminChallengeDto(raw, (int)ChallengeLifetime.TotalSeconds), null);
        }

        admin.RecordSuccessfulLogin(now);
        audit.Record(AuditActorType.Admin, admin.Id, "admin.login.success", "AdminUser", admin.Id);
        var pair = await sessions.StartSessionAsync(admin, ct);   // saves everything above
        return new AdminLoginResultDto(false, null, await profiles.ToAuthResultAsync(pair, ct));
    }
}

// ======================================================================= 2FA verify (step 2 of login)
public sealed record AdminVerifyTwoFactorCommand(string ChallengeToken, string Code) : IRequest<Result<AdminAuthResultDto>>;

public sealed class AdminVerifyTwoFactorValidator : AbstractValidator<AdminVerifyTwoFactorCommand>
{
    public AdminVerifyTwoFactorValidator()
    {
        RuleFor(x => x.ChallengeToken).MustBeRequired(200);
        RuleFor(x => x.Code).NotEmpty().WithErrorCode("REQUIRED")
            .Must(AdminCodes.IsSixDigits).WithErrorCode("ADMIN_2FA_CODE_INVALID");
    }
}

public sealed class AdminVerifyTwoFactorHandler(
    IApplicationDbContext db, IClock clock, ITotpService totp, IFieldProtector protector,
    AdminSessionService sessions, AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<AdminVerifyTwoFactorCommand, Result<AdminAuthResultDto>>
{
    public const int MaxChallengeAttempts = 5;

    public async Task<Result<AdminAuthResultDto>> Handle(AdminVerifyTwoFactorCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = TokenHasher.Hash(cmd.ChallengeToken.Trim());
        var challenge = await db.AdminTwoFactorChallenges.FirstOrDefaultAsync(c => c.TokenHash == hash && c.ConsumedAt == null, ct);
        if (challenge is null || challenge.ExpiresAt <= now || challenge.Attempts >= MaxChallengeAttempts)
            return AdminErrors.TwoFactorChallengeInvalid;

        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == challenge.AdminUserId, ct);
        if (admin is null || admin.Status != AdminStatus.Active || !admin.TwoFactorEnabled || string.IsNullOrEmpty(admin.TwoFactorSecret))
        {
            challenge.ConsumedAt = now;
            await db.SaveChangesAsync(ct);
            return AdminErrors.TwoFactorChallengeInvalid;
        }

        if (admin.IsLocked(now)) return AdminErrors.AccountLocked(admin.RemainingLockSeconds(now));

        if (!totp.Verify(protector.Unprotect(admin.TwoFactorSecret), cmd.Code))
        {
            challenge.Attempts++;
            var locked = admin.RecordFailedLogin(now);   // a wrong code counts like a wrong password
            audit.Record(AuditActorType.Admin, admin.Id, "admin.login.failed", "AdminUser", admin.Id, null, new { reason = "wrong_2fa_code" });
            if (locked) audit.Record(AuditActorType.Admin, admin.Id, "admin.account.locked", "AdminUser", admin.Id);
            await db.SaveChangesAsync(ct);
            return locked ? AdminErrors.AccountLocked((int)AdminUser.LockoutDuration.TotalSeconds) : AdminErrors.TwoFactorCodeInvalid;
        }

        challenge.ConsumedAt = now;   // single use
        admin.RecordSuccessfulLogin(now);
        audit.Record(AuditActorType.Admin, admin.Id, "admin.login.success", "AdminUser", admin.Id, null, new { twoFactor = true });
        var pair = await sessions.StartSessionAsync(admin, ct);
        return await profiles.ToAuthResultAsync(pair, ct);
    }
}

// ======================================================================= Refresh
public sealed record AdminRefreshCommand(string RefreshToken) : IRequest<Result<AdminAuthResultDto>>;

public sealed class AdminRefreshValidator : AbstractValidator<AdminRefreshCommand>
{
    public AdminRefreshValidator() => RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("REQUIRED");
}

public sealed class AdminRefreshHandler(AdminSessionService sessions, AdminProfileService profiles)
    : IRequestHandler<AdminRefreshCommand, Result<AdminAuthResultDto>>
{
    public async Task<Result<AdminAuthResultDto>> Handle(AdminRefreshCommand cmd, CancellationToken ct)
    {
        var rotated = await sessions.RotateAsync(cmd.RefreshToken, ct);
        if (rotated.IsFailure) return rotated.Error;
        return await profiles.ToAuthResultAsync(rotated.Value, ct);
    }
}

// ======================================================================= Logout / logout-all
public sealed record AdminLogoutCommand(string? RefreshToken) : IRequest<Result>;

public sealed class AdminLogoutHandler(
    IApplicationDbContext db, ICurrentAdmin current, AdminSessionService sessions, IClock clock, AdminAuditService audit)
    : IRequestHandler<AdminLogoutCommand, Result>
{
    public async Task<Result> Handle(AdminLogoutCommand cmd, CancellationToken ct)
    {
        if (current.AdminId is not { } adminId) return CommonErrors.Unauthorized;

        if (current.SessionId is { } sessionId)
            await sessions.RevokeFamilyAsync(adminId, sessionId, RefreshRevokeReason.Logout, ct);

        if (!string.IsNullOrWhiteSpace(cmd.RefreshToken))
        {
            var hash = TokenHasher.Hash(cmd.RefreshToken);
            var token = await db.AdminRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.AdminUserId == adminId, ct);
            token?.Revoke(clock.UtcNow, RefreshRevokeReason.Logout);
        }

        audit.Record(AuditActorType.Admin, adminId, "admin.logout", "AdminUser", adminId);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record AdminLogoutAllCommand : IRequest<Result>;

public sealed class AdminLogoutAllHandler(
    IApplicationDbContext db, ICurrentAdmin current, AdminSessionService sessions, AdminAuditService audit)
    : IRequestHandler<AdminLogoutAllCommand, Result>
{
    public async Task<Result> Handle(AdminLogoutAllCommand cmd, CancellationToken ct)
    {
        if (current.AdminId is not { } adminId) return CommonErrors.Unauthorized;

        await sessions.RevokeAllAsync(adminId, RefreshRevokeReason.LogoutAll, null, ct);
        audit.Record(AuditActorType.Admin, adminId, "admin.logout_all", "AdminUser", adminId);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ======================================================================= Me
public sealed record AdminMeQuery : IRequest<Result<AdminDto>>;

public sealed class AdminMeHandler(AdminAccessService access, AdminProfileService profiles)
    : IRequestHandler<AdminMeQuery, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(AdminMeQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(null, false, ct);
        if (guard.IsFailure) return guard.Error;
        return await profiles.BuildAsync(guard.Value, ct);
    }
}

// ======================================================================= 2FA setup / enable / disable
public sealed record AdminTwoFactorSetupCommand : IRequest<Result<AdminTwoFactorSetupDto>>;

public sealed class AdminTwoFactorSetupHandler(
    IApplicationDbContext db, AdminAccessService access, ITotpService totp, IFieldProtector protector, AdminAuditService audit)
    : IRequestHandler<AdminTwoFactorSetupCommand, Result<AdminTwoFactorSetupDto>>
{
    public const string Issuer = "Etamid Admin";

    public async Task<Result<AdminTwoFactorSetupDto>> Handle(AdminTwoFactorSetupCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(null, false, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;
        if (admin.TwoFactorEnabled) return AdminErrors.TwoFactorAlreadyEnabled;

        // Stored encrypted but NOT active until /2fa/enable proves the authenticator works.
        var secret = totp.NewSecret();
        admin.TwoFactorSecret = protector.Protect(secret);
        audit.Record(AuditActorType.Admin, admin.Id, "admin.2fa.setup", "AdminUser", admin.Id);
        await db.SaveChangesAsync(ct);
        return new AdminTwoFactorSetupDto(secret, totp.BuildOtpAuthUri(Issuer, admin.Email, secret));
    }
}

public sealed record AdminTwoFactorEnableCommand(string Code) : IRequest<Result<AdminDto>>;

public sealed class AdminTwoFactorEnableValidator : AbstractValidator<AdminTwoFactorEnableCommand>
{
    public AdminTwoFactorEnableValidator()
        => RuleFor(x => x.Code).NotEmpty().WithErrorCode("REQUIRED").Must(AdminCodes.IsSixDigits).WithErrorCode("ADMIN_2FA_CODE_INVALID");
}

public sealed class AdminTwoFactorEnableHandler(
    IApplicationDbContext db, AdminAccessService access, ITotpService totp, IFieldProtector protector,
    AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<AdminTwoFactorEnableCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(AdminTwoFactorEnableCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(null, false, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;
        if (admin.TwoFactorEnabled) return AdminErrors.TwoFactorAlreadyEnabled;
        if (string.IsNullOrEmpty(admin.TwoFactorSecret)) return AdminErrors.TwoFactorSetupRequired;

        if (!totp.Verify(protector.Unprotect(admin.TwoFactorSecret), cmd.Code)) return AdminErrors.TwoFactorCodeInvalid;

        admin.TwoFactorEnabled = true;
        audit.Record(AuditActorType.Admin, admin.Id, "admin.2fa.enable", "AdminUser", admin.Id, new { twoFactorEnabled = false }, new { twoFactorEnabled = true });
        await db.SaveChangesAsync(ct);
        return await profiles.BuildAsync(admin, ct);
    }
}

/// <summary>Turning 2FA off needs the current password AND a current code. Failures count toward lockout.</summary>
public sealed record AdminTwoFactorDisableCommand(string Password, string Code) : IRequest<Result<AdminDto>>;

public sealed class AdminTwoFactorDisableValidator : AbstractValidator<AdminTwoFactorDisableCommand>
{
    public AdminTwoFactorDisableValidator()
    {
        RuleFor(x => x.Password).MustBeRequired(200);
        RuleFor(x => x.Code).NotEmpty().WithErrorCode("REQUIRED").Must(AdminCodes.IsSixDigits).WithErrorCode("ADMIN_2FA_CODE_INVALID");
    }
}

public sealed class AdminTwoFactorDisableHandler(
    IApplicationDbContext db, AdminAccessService access, IPasswordHasher hasher, ITotpService totp, IFieldProtector protector,
    IClock clock, AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<AdminTwoFactorDisableCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(AdminTwoFactorDisableCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(null, false, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;
        if (!admin.TwoFactorEnabled || string.IsNullOrEmpty(admin.TwoFactorSecret)) return AdminErrors.TwoFactorNotEnabled;

        var now = clock.UtcNow;
        if (admin.IsLocked(now)) return AdminErrors.AccountLocked(admin.RemainingLockSeconds(now));

        if (!hasher.Verify(admin.PasswordHash, cmd.Password) || !totp.Verify(protector.Unprotect(admin.TwoFactorSecret), cmd.Code))
        {
            var locked = admin.RecordFailedLogin(now);
            audit.Record(AuditActorType.Admin, admin.Id, "admin.2fa.disable_failed", "AdminUser", admin.Id);
            await db.SaveChangesAsync(ct);
            return locked ? AdminErrors.AccountLocked((int)AdminUser.LockoutDuration.TotalSeconds) : AdminErrors.InvalidCredentials;
        }

        admin.TwoFactorEnabled = false;
        admin.TwoFactorSecret = null;
        audit.Record(AuditActorType.Admin, admin.Id, "admin.2fa.disable", "AdminUser", admin.Id, new { twoFactorEnabled = true }, new { twoFactorEnabled = false });
        await db.SaveChangesAsync(ct);
        return await profiles.BuildAsync(admin, ct);
    }
}
