using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
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

/// <summary>Forgot password: the app proves phone ownership with Firebase, then sets a new password here.</summary>
public sealed record ResetPasswordCommand(string Phone, string FirebaseIdToken, string NewPassword, string ConfirmPassword)
    : IRequest<Result>;

public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Phone).MustBeSaudiPhone();
        RuleFor(x => x.FirebaseIdToken).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.NewPassword).MustBeStrongPassword();
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithErrorCode("PASSWORD_MISMATCH");
    }
}

public sealed class ResetPasswordHandler(
    IApplicationDbContext db,
    IPhoneVerifier verifier,
    IPasswordHasher hasher,
    IClock clock,
    SessionService sessions,
    AuthAuditService audit) : IRequestHandler<ResetPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetPasswordCommand r, CancellationToken ct)
    {
        // Unknown number, wrong number and bad token all give the same answer.
        if (!PhoneNumber.TryParse(r.Phone, out var phone))
            return IdentityErrors.PhoneTokenInvalid;

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null && u.Status == AccountStatus.Active, ct);
        if (user is null)
            return IdentityErrors.PhoneTokenInvalid;

        var verified = await verifier.VerifyAsync(r.FirebaseIdToken, ct);
        var now = clock.UtcNow;

        if (verified.IsFailure
            || verified.Value.PhoneE164 != phone.E164
            || !PhoneVerificationPolicy.IsFresh(verified.Value.AuthTimeUtc, now)
            || PhoneVerificationPolicy.IsReplay(verified.Value.AuthTimeUtc, user.LastPhoneAuthAt))
        {
            audit.Record(AuthEvent.PhoneVerificationFailed, user.Id, user.PhoneNormalized);
            await db.SaveChangesAsync(ct);
            return IdentityErrors.PhoneTokenInvalid;
        }

        user.AcceptPhoneAuth(verified.Value.AuthTimeUtc);
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
