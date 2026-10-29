using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Errors;
using Etamid.Application.Identity.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.Services;
using Etamid.Domain.Identity.ValueObjects;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

// ---------- Forgot password: 3 steps
// 1) forgot      -> a code is emailed (the answer never reveals whether the account exists)
// 2) verify-otp  -> the code is exchanged for a single-use reset token
// 3) reset       -> the token + the new password

public sealed record ForgotPasswordCommand(string Phone) : IRequest<Result>;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(x => x.Phone).MustBeSaudiPhone();
}

public sealed class ForgotPasswordHandler(IApplicationDbContext db, EmailOtpService otps, AuthAuditService audit)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand r, CancellationToken ct)
    {
        if (!PhoneNumber.TryParse(r.Phone, out var phone)) return Result.Success();

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null && u.Status == AccountStatus.Active, ct);
        if (user is null) return Result.Success();

        // A cooldown refusal is also hidden: the caller only ever sees "sent".
        var issued = await otps.IssueAsync(user, OtpPurpose.PasswordReset, ct);
        if (issued.IsSuccess)
        {
            audit.Record(AuthEvent.PasswordResetRequested, user.Id, user.PhoneNormalized);
            await db.SaveChangesAsync(ct);
        }

        return Result.Success();
    }
}

public sealed record VerifyResetCodeCommand(string Phone, string Code) : IRequest<Result<ResetTokenDto>>;

public sealed class VerifyResetCodeValidator : AbstractValidator<VerifyResetCodeCommand>
{
    public VerifyResetCodeValidator()
    {
        RuleFor(x => x.Phone).MustBeSaudiPhone();
        RuleFor(x => x.Code)
            .NotEmpty().WithErrorCode("REQUIRED")
            .Must(EmailOtpPolicy.IsWellFormed).WithErrorCode("OTP_INVALID");
    }
}

public sealed class VerifyResetCodeHandler(
    IApplicationDbContext db, EmailOtpService otps, IClock clock, AuthAuditService audit)
    : IRequestHandler<VerifyResetCodeCommand, Result<ResetTokenDto>>
{
    public async Task<Result<ResetTokenDto>> Handle(VerifyResetCodeCommand r, CancellationToken ct)
    {
        if (!PhoneNumber.TryParse(r.Phone, out var phone)) return IdentityErrors.OtpInvalid;

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null && u.Status == AccountStatus.Active, ct);
        if (user is null) return IdentityErrors.OtpInvalid;

        var check = await otps.CheckAsync(user, OtpPurpose.PasswordReset, r.Code, ct);
        if (check.IsFailure)
        {
            audit.Record(AuthEvent.EmailVerificationFailed, user.Id, user.PhoneNormalized);
            await db.SaveChangesAsync(ct);
            return check.Error;
        }

        var now = clock.UtcNow;
        var token = TokenHasher.NewToken();

        var otp = check.Value;
        otp.ConsumedAt = now;
        otp.ResetTokenHash = TokenHasher.Hash(token);
        otp.ResetTokenExpiresAt = now + EmailOtpPolicy.ResetTokenLifetime;
        await db.SaveChangesAsync(ct);

        return new ResetTokenDto(token, (int)EmailOtpPolicy.ResetTokenLifetime.TotalSeconds);
    }
}

public sealed record ResetPasswordCommand(string ResetToken, string NewPassword, string ConfirmPassword) : IRequest<Result>;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.ResetToken).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.NewPassword).MustBeStrongPassword();
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithErrorCode("PASSWORD_MISMATCH");
    }
}

public sealed class ResetPasswordHandler(
    IApplicationDbContext db,
    IPasswordHasher hasher,
    IClock clock,
    SessionService sessions,
    AuthAuditService audit) : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand r, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tokenHash = TokenHasher.Hash(r.ResetToken.Trim());

        var otp = await db.EmailOtps.FirstOrDefaultAsync(
            o => o.ResetTokenHash == tokenHash && o.Purpose == OtpPurpose.PasswordReset && o.ResetTokenUsedAt == null, ct);
        if (otp is null || otp.ResetTokenExpiresAt is null || otp.ResetTokenExpiresAt <= now)
            return IdentityErrors.ResetTokenInvalid;

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Id == otp.UserId && u.DeletedAt == null && u.Status == AccountStatus.Active, ct);
        if (user is null)
            return IdentityErrors.ResetTokenInvalid;

        otp.ResetTokenUsedAt = now;
        user.ChangePassword(hasher.Hash(r.NewPassword));
        audit.Record(AuthEvent.PasswordReset, user.Id, user.PhoneNormalized);
        await db.SaveChangesAsync(ct);

        await sessions.RevokeAllAsync(user.Id, RefreshRevokeReason.PasswordReset, null, ct);
        return Result.Success();
    }
}

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.NewPassword).MustBeStrongPassword();
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword).WithErrorCode("PASSWORD_SAME_AS_CURRENT");
    }
}

public sealed class ChangePasswordHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    SessionService sessions,
    AuthAuditService audit) : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        if (!hasher.Verify(user.PasswordHash, r.CurrentPassword))
            return IdentityErrors.WrongCurrentPassword;

        user.ChangePassword(hasher.Hash(r.NewPassword));
        audit.Record(AuthEvent.PasswordChanged, user.Id, user.PhoneNormalized);
        await db.SaveChangesAsync(ct);

        // Every other device must sign in again; this one stays signed in.
        await sessions.RevokeAllAsync(user.Id, RefreshRevokeReason.PasswordChanged, currentUser.SessionId, ct);
        return Result.Success();
    }
}
