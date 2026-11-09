using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Errors;
using Etamid.Application.Identity.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.ValueObjects;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record LoginCommand(string Phone, string Password) : IRequest<Result<AuthResultDto>>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("REQUIRED");
    }
}

public sealed class LoginHandler(
    IApplicationDbContext db,
    IPasswordHasher hasher,
    IClock clock,
    SessionService sessions,
    UserProfileService profiles,
    AuthAuditService audit) : IRequestHandler<LoginCommand, Result<AuthResultDto>>
{
    public async Task<Result<AuthResultDto>> Handle(LoginCommand r, CancellationToken ct)
    {
        // Login failures are specific on purpose (owner decision 2026-11-09):
        // an unregistered phone and a wrong password get different codes, so
        // phone registration is probeable by design. The dummy verify is
        // still done on the unknown-phone path so the timing does not leak
        // the answer any faster than the message does.
        if (!PhoneNumber.TryParse(r.Phone, out var phone))
        {
            hasher.VerifyDummy(r.Password);
            return IdentityErrors.InvalidPhone;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null, ct);
        if (user is null)
        {
            hasher.VerifyDummy(r.Password);
            return IdentityErrors.PhoneNotRegistered;
        }

        var now = clock.UtcNow;

        if (user.IsLocked(now))
            return IdentityErrors.AccountLocked(user.RemainingLockSeconds(now));

        if (!hasher.Verify(user.PasswordHash, r.Password))
        {
            var locked = user.RecordFailedLogin(now);
            audit.Record(AuthEvent.LoginFailed, user.Id, user.PhoneNormalized);
            if (locked) audit.Record(AuthEvent.AccountLocked, user.Id, user.PhoneNormalized);
            await db.SaveChangesAsync(ct);

            return locked
                ? IdentityErrors.AccountLocked((int)User.LockoutDuration.TotalSeconds)
                : IdentityErrors.WrongPassword;
        }

        if (user.Status == AccountStatus.PendingVerification) return IdentityErrors.AccountNotVerified;
        if (user.Status != AccountStatus.Active) return IdentityErrors.AccountSuspended;

        user.RecordSuccessfulLogin(now);
        audit.Record(AuthEvent.LoginSuccess, user.Id, user.PhoneNormalized);

        var pair = await sessions.StartSessionAsync(user, ct);   // saves everything above
        return await profiles.ToAuthResultAsync(pair, ct);
    }
}
