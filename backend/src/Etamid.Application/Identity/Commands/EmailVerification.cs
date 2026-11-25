using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
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

/// <summary>Step 2 of registration: the 6-digit code from the email activates the account and signs the user in.</summary>
public sealed record VerifyEmailCommand(string Phone, string Code) : IRequest<Result<AuthResultDto>>;

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.Phone).MustBeSaudiPhone();
        RuleFor(x => x.Code)
            .NotEmpty().WithErrorCode("REQUIRED")
            .Must(EmailOtpPolicy.IsWellFormed).WithErrorCode("OTP_INVALID");
    }
}

public sealed class VerifyEmailHandler(
    IApplicationDbContext db,
    EmailOtpService otps,
    IClock clock,
    SessionService sessions,
    UserProfileService profiles,
    AuthAuditService audit,
    EscrowLedgerService ledger) : IRequestHandler<VerifyEmailCommand, Result<AuthResultDto>>
{
    public async Task<Result<AuthResultDto>> Handle(VerifyEmailCommand r, CancellationToken ct)
    {
        // Like login, the failure names the actual problem instead of
        // blaming the code (owner decision 2026-11-09).
        if (!PhoneNumber.TryParse(r.Phone, out var phone))
            return IdentityErrors.InvalidPhone;

        var user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null, ct);
        if (user is null)
            return IdentityErrors.PhoneNotRegistered;

        if (user.Status is not (AccountStatus.PendingVerification or AccountStatus.Active))
            return IdentityErrors.AccountNotVerified;
        if (user.EmailVerifiedAt is not null)
            return IdentityErrors.AccountAlreadyVerified;

        var check = await otps.CheckAsync(user, OtpPurpose.Registration, r.Code, ct);
        if (check.IsFailure)
        {
            audit.Record(AuthEvent.EmailVerificationFailed, user.Id, user.PhoneNormalized);
            await db.SaveChangesAsync(ct);
            return check.Error;
        }

        var now = clock.UtcNow;
        check.Value.ConsumedAt = now;
        user.VerifyEmail(now);
        audit.Record(AuthEvent.EmailVerified, user.Id, user.PhoneNormalized);

        // Same as login: the wallet is active from the moment the user enters
        // the account (registration signs the user straight in, so this is
        // the new user's first entry). Best-effort; the manual endpoint stays.
        try
        {
            var wallet = await ledger.GetOrCreateWalletAsync(user.Id, ct);
            if (!wallet.Activated)
            {
                wallet.Activated = true;
                wallet.ActivatedAt = now;
            }
        }
        catch
        {
            // Activation stays pending; the manual endpoint remains available.
        }

        var pair = await sessions.StartSessionAsync(user, ct);   // saves everything above
        return await profiles.ToAuthResultAsync(pair, ct);
    }
}

/// <summary>Sends a fresh code to any existing account, verified or not, with no resend cooldown
/// (owner decision 2026-11-17). Unknown numbers still get a silent success: there is no email
/// address to send a code to, and answering differently would let callers enumerate registered phones.</summary>
public sealed record ResendEmailCodeCommand(string Phone) : IRequest<Result>;

public sealed class ResendEmailCodeValidator : AbstractValidator<ResendEmailCodeCommand>
{
    public ResendEmailCodeValidator() => RuleFor(x => x.Phone).MustBeSaudiPhone();
}

public sealed class ResendEmailCodeHandler(IApplicationDbContext db, EmailOtpService otps)
    : IRequestHandler<ResendEmailCodeCommand, Result>
{
    public async Task<Result> Handle(ResendEmailCodeCommand r, CancellationToken ct)
    {
        // Unknown numbers get the same answer as a successful send: there is no email
        // address to send a code to, and answering differently would let callers
        // enumerate registered phones.
        if (!PhoneNumber.TryParse(r.Phone, out var phone)) return Result.Success();

        var user = await db.Users.FirstOrDefaultAsync(
            u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null, ct);
        if (user is null) return Result.Success();

        // Always send a fresh code, verified or not, with no cooldown (owner decision 2026-11-17).
        return await otps.IssueAsync(user, OtpPurpose.Registration, ct, skipCooldown: true);
    }
}
