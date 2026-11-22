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

/// <summary>
/// The edit-profile screen saves everything in one call. <c>CurrentPassword</c> + <c>NewPassword</c> are optional and
/// travel together: when present the password is changed too (all other devices are signed out, this one stays).
/// Nothing is saved unless every part is valid.
/// </summary>
public sealed record UpdateProfileCommand(
    string? Username, string? Email, string? Phone = null,
    string? CurrentPassword = null, string? NewPassword = null) : IRequest<Result<UpdateProfileResultDto>>;

/// <summary>
/// Result of a profile update. When the email changed, the new address is
/// unverified: <c>EmailVerificationRequired</c> is true and the caller should
/// send the user to the existing OTP verification flow
/// (<c>POST /auth/otp/verify</c> with phone + code, or <c>POST /auth/otp/resend</c>).
/// <c>OtpSent</c> is false when the code could not be sent (e.g. cooldown or
/// SMTP not configured) — the update itself still succeeded, and resend remains available.
/// <c>PhoneChanged</c> is true when the login phone changed: from then on the user signs in with the
/// new number (and email-OTP calls take the new number).
/// </summary>
public sealed record UpdateProfileResultDto(bool EmailChanged, bool EmailVerificationRequired, bool OtpSent, bool PhoneChanged = false, bool PasswordChanged = false);

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Username!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Username is not null);
        RuleFor(x => x.Email!).MustBeEmail().When(x => x.Email is not null);
        RuleFor(x => x.Phone!).MustBeSaudiPhone().When(x => x.Phone is not null);

        // A password change needs both fields; sending only one of them is a mistake, not a no-op.
        RuleFor(x => x.CurrentPassword).NotEmpty().WithErrorCode("REQUIRED").When(x => x.NewPassword is not null);
        RuleFor(x => x.NewPassword).NotEmpty().WithErrorCode("REQUIRED").When(x => x.CurrentPassword is not null);
        RuleFor(x => x.NewPassword!).MustBeStrongPassword().When(x => !string.IsNullOrEmpty(x.NewPassword));
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword).WithErrorCode("PASSWORD_SAME_AS_CURRENT")
            .When(x => !string.IsNullOrEmpty(x.NewPassword) && !string.IsNullOrEmpty(x.CurrentPassword));
    }
}

public sealed class UpdateProfileHandler(
    IApplicationDbContext db, ICurrentUser currentUser, EmailOtpService otps, AuthAuditService audit,
    IPasswordHasher hasher, SessionService sessions)
    : IRequestHandler<UpdateProfileCommand, Result<UpdateProfileResultDto>>
{
    public async Task<Result<UpdateProfileResultDto>> Handle(UpdateProfileCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        // Checked first so a wrong current password rejects the whole save, not just the password part.
        var passwordChanging = !string.IsNullOrEmpty(r.NewPassword);
        if (passwordChanging && !hasher.Verify(user.PasswordHash, r.CurrentPassword!))
            return IdentityErrors.WrongCurrentPassword;

        var emailChanged = false;
        if (!string.IsNullOrWhiteSpace(r.Email))
        {
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email && u.Id != userId && u.DeletedAt == null, ct))
                return IdentityErrors.EmailExists;
            emailChanged = !string.Equals(email, user.Email, StringComparison.Ordinal);
        }

        // The phone is the login identity, so it must stay unique among every live account, including
        // unverified ones (an unverified owner would otherwise lose the number to a profile edit).
        var phoneChanged = false;
        if (!string.IsNullOrWhiteSpace(r.Phone))
        {
            if (!PhoneNumber.TryParse(r.Phone, out var phone)) return IdentityErrors.InvalidPhone;

            if (!string.Equals(phone.E164, user.PhoneNormalized, StringComparison.Ordinal))
            {
                if (await db.Users.AnyAsync(u => u.PhoneNormalized == phone.E164 && u.Id != userId && u.DeletedAt == null, ct))
                    return IdentityErrors.PhoneExists;

                var previous = user.PhoneNormalized;
                phoneChanged = user.ChangePhone(phone);
                audit.Record(AuthEvent.PhoneChanged, user.Id, user.PhoneNormalized, $"from {previous}");
            }
        }

        user.UpdateProfile(r.Username, r.Email);
        if (passwordChanging)
        {
            user.ChangePassword(hasher.Hash(r.NewPassword!));
            audit.Record(AuthEvent.PasswordChanged, user.Id, user.PhoneNormalized);
        }
        await db.SaveChangesAsync(ct);

        // Every other device must sign in again; this one stays signed in.
        if (passwordChanging)
            await sessions.RevokeAllAsync(user.Id, RefreshRevokeReason.PasswordChanged, currentUser.SessionId, ct);

        // A changed address is unverified (see User.UpdateProfile). Reuse the
        // existing registration OTP flow: VerifyEmailHandler already accepts an
        // Active user whose EmailVerifiedAt is null, and ResendEmailCodeHandler
        // already sends to such a user. Sending here is best-effort: a cooldown
        // or missing SMTP config must not roll back the profile update.
        var otpSent = false;
        if (emailChanged)
        {
            try
            {
                otpSent = (await otps.IssueAsync(user, OtpPurpose.Registration, ct)).IsSuccess;
            }
            catch
            {
                otpSent = false;
            }
        }

        return new UpdateProfileResultDto(emailChanged, emailChanged, otpSent, phoneChanged, passwordChanging);
    }
}

public sealed record ChangeLanguageCommand(string Language) : IRequest<Result>;

public sealed class ChangeLanguageValidator : AbstractValidator<ChangeLanguageCommand>
{
    public ChangeLanguageValidator()
        => RuleFor(x => x.Language)
            .Must(v => Enum.TryParse<Etamid.Domain.Identity.Language>(v, true, out _))
            .WithErrorCode("INVALID_VALUE");
}

public sealed class ChangeLanguageHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ChangeLanguageCommand, Result>
{
    public async Task<Result> Handle(ChangeLanguageCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        user.ChangeLanguage(Enum.Parse<Etamid.Domain.Identity.Language>(r.Language, true));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
