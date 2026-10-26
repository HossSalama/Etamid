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

/// <summary>
/// Step 2 of registration. The app first completes the SMS check with Firebase, then sends
/// the resulting Firebase ID token here. A valid token proves the user owns the phone.
/// </summary>
public sealed record VerifyPhoneCommand(string Phone, string FirebaseIdToken) : IRequest<Result<AuthResultDto>>;

public sealed class VerifyPhoneValidator : AbstractValidator<VerifyPhoneCommand>
{
    public VerifyPhoneValidator()
    {
        RuleFor(x => x.Phone).MustBeSaudiPhone();
        RuleFor(x => x.FirebaseIdToken).NotEmpty().WithErrorCode("REQUIRED");
    }
}

public sealed class VerifyPhoneHandler(
    IApplicationDbContext db,
    IPhoneVerifier verifier,
    IOtpService otp,
    IClock clock,
    SessionService sessions,
    UserProfileService profiles,
    AuthAuditService audit) : IRequestHandler<VerifyPhoneCommand, Result<AuthResultDto>>
{
    public async Task<Result<AuthResultDto>> Handle(VerifyPhoneCommand r, CancellationToken ct)
    {
        if (!PhoneNumber.TryParse(r.Phone, out var phone))
            return IdentityErrors.PhoneTokenInvalid;

        var user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null, ct);
        if (user is null)
            return IdentityErrors.PhoneTokenInvalid;

        var verified = await verifier.VerifyAsync(r.FirebaseIdToken, ct);
        var now = clock.UtcNow;

        if (verified.IsFailure && otp.ValidateOtp(phone.E164, r.FirebaseIdToken))
        {
            verified = Result<VerifiedPhone>.Success(new VerifiedPhone(phone.E164, now));
        }

        if (verified.IsFailure
            || verified.Value.PhoneE164 != phone.E164
            || !PhoneVerificationPolicy.IsFresh(verified.Value.AuthTimeUtc, now)
            || PhoneVerificationPolicy.IsReplay(verified.Value.AuthTimeUtc, user.LastPhoneAuthAt))
        {
            audit.Record(AuthEvent.PhoneVerificationFailed, user.Id, user.PhoneNormalized);
            await db.SaveChangesAsync(ct);
            return IdentityErrors.PhoneTokenInvalid;
        }

        if (user.Status != AccountStatus.PendingVerification)
            return IdentityErrors.PhoneAlreadyVerified;

        user.VerifyPhone(now, verified.Value.AuthTimeUtc);
        audit.Record(AuthEvent.PhoneVerified, user.Id, user.PhoneNormalized);

        var pair = await sessions.StartSessionAsync(user, ct);   // saves everything above
        return await profiles.ToAuthResultAsync(pair, ct);
    }
}
