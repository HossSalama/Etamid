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

/// <summary>Supplemental phone verification. Email activation must happen first.</summary>
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

        if (user.Status != AccountStatus.Active || user.EmailVerifiedAt is null)
            return IdentityErrors.AccountNotVerified;
        if (user.PhoneVerifiedAt is not null)
            return IdentityErrors.PhoneAlreadyVerified;

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

        user.VerifyPhone(now, verified.Value.AuthTimeUtc);
        audit.Record(AuthEvent.PhoneVerified, user.Id, user.PhoneNormalized);

        var pair = await sessions.StartSessionAsync(user, ct);
        return await profiles.ToAuthResultAsync(pair, ct);
    }
}
