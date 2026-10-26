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

public sealed record VerifyOtpCommand(string Phone, string Code) : IRequest<Result<AuthResultDto>>;

public sealed class VerifyOtpValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpValidator()
    {
        RuleFor(x => x.Phone).MustBeSaudiPhone();
        RuleFor(x => x.Code).NotEmpty().WithErrorCode("REQUIRED").Length(6).WithErrorCode("INVALID_CODE");
    }
}

public sealed class VerifyOtpHandler(
    IApplicationDbContext db,
    IOtpService otp,
    IClock clock,
    SessionService sessions,
    UserProfileService profiles,
    AuthAuditService audit) : IRequestHandler<VerifyOtpCommand, Result<AuthResultDto>>
{
    public async Task<Result<AuthResultDto>> Handle(VerifyOtpCommand r, CancellationToken ct)
    {
        if (!PhoneNumber.TryParse(r.Phone, out var phone))
            return IdentityErrors.PhoneTokenInvalid;

        var user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null, ct);
        if (user is null)
            return IdentityErrors.PhoneTokenInvalid;

        if (user.Status != AccountStatus.PendingVerification)
            return IdentityErrors.PhoneAlreadyVerified;

        if (!otp.ValidateOtp(phone.E164, r.Code))
        {
            audit.Record(AuthEvent.PhoneVerificationFailed, user.Id, user.PhoneNormalized);
            await db.SaveChangesAsync(ct);
            return IdentityErrors.PhoneTokenInvalid;
        }

        var now = clock.UtcNow;
        user.VerifyPhone(now, now);
        audit.Record(AuthEvent.PhoneVerified, user.Id, user.PhoneNormalized);

        var pair = await sessions.StartSessionAsync(user, ct);
        return await profiles.ToAuthResultAsync(pair, ct);
    }
}

public sealed record ResendOtpCommand(string Phone, string? Purpose) : IRequest<Result<ResendOtpResultDto>>;

public sealed class ResendOtpValidator : AbstractValidator<ResendOtpCommand>
{
    public ResendOtpValidator()
    {
        RuleFor(x => x.Phone).MustBeSaudiPhone();
    }
}

public sealed class ResendOtpHandler(
    IApplicationDbContext db,
    IOtpService otp) : IRequestHandler<ResendOtpCommand, Result<ResendOtpResultDto>>
{
    public async Task<Result<ResendOtpResultDto>> Handle(ResendOtpCommand r, CancellationToken ct)
    {
        if (!PhoneNumber.TryParse(r.Phone, out var phone))
            return IdentityErrors.InvalidPhone;

        var user = await db.Users.FirstOrDefaultAsync(u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null, ct);
        if (user is null)
            return new ResendOtpResultDto(60);

        await otp.GenerateAndSendOtpAsync(phone.E164, user.Email, user.Name, ct);
        return new ResendOtpResultDto(60);
    }
}
