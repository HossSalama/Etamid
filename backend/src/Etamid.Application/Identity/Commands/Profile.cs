using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Errors;
using Etamid.Application.Identity.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record UpdateProfileCommand(string? Username, string? Email) : IRequest<Result<UpdateProfileResultDto>>;

/// <summary>
/// Result of a profile update. When the email changed, the new address is
/// unverified: <c>EmailVerificationRequired</c> is true and the caller should
/// send the user to the existing OTP verification flow
/// (<c>POST /auth/otp/verify</c> with phone + code, or <c>POST /auth/otp/resend</c>).
/// <c>OtpSent</c> is false when the code could not be sent (e.g. cooldown or
/// SMTP not configured) — the update itself still succeeded, and resend remains available.
/// </summary>
public sealed record UpdateProfileResultDto(bool EmailChanged, bool EmailVerificationRequired, bool OtpSent);

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Username!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Username is not null);
        RuleFor(x => x.Email!).MustBeEmail().When(x => x.Email is not null);
    }
}

public sealed class UpdateProfileHandler(
    IApplicationDbContext db, ICurrentUser currentUser, EmailOtpService otps)
    : IRequestHandler<UpdateProfileCommand, Result<UpdateProfileResultDto>>
{
    public async Task<Result<UpdateProfileResultDto>> Handle(UpdateProfileCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        var emailChanged = false;
        if (!string.IsNullOrWhiteSpace(r.Email))
        {
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email && u.Id != userId && u.DeletedAt == null, ct))
                return IdentityErrors.EmailExists;
            emailChanged = !string.Equals(email, user.Email, StringComparison.Ordinal);
        }

        user.UpdateProfile(r.Username, r.Email);
        await db.SaveChangesAsync(ct);

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

        return new UpdateProfileResultDto(emailChanged, emailChanged, otpSent);
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
