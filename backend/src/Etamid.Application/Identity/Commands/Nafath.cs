using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record NafathStartDto(string RequestId, string RandomNumber, int ExpiresInSeconds);

public sealed record NafathStatusDto(NafathStatus Status);

public sealed record StartNafathCommand : IRequest<Result<NafathStartDto>>;

public sealed class StartNafathHandler(IApplicationDbContext db, ICurrentUser currentUser, INafathClient nafath, IClock clock)
    : IRequestHandler<StartNafathCommand, Result<NafathStartDto>>
{
    public const int ExpirySeconds = 120;

    public async Task<Result<NafathStartDto>> Handle(StartNafathCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return Etamid.Application.Identity.Errors.IdentityErrors.UserNotFound;
        if (user.NafathVerified) return Error.Conflict("NAFATH_ALREADY_VERIFIED");

        var now = clock.UtcNow;

        // Idempotent: while a request is still pending the same number is returned.
        var open = await db.NafathVerifications
            .Where(n => n.UserId == userId && n.Status == NafathStatus.Pending && n.ExpiresAt > now)
            .OrderByDescending(n => n.Id)
            .FirstOrDefaultAsync(ct);
        if (open is not null)
            return new NafathStartDto(open.RequestId, open.RandomNumber, (int)Math.Max(1, (open.ExpiresAt - now).TotalSeconds));

        var requestId = Guid.NewGuid().ToString("N");
        var started = await nafath.StartAsync(requestId, ct);

        db.NafathVerifications.Add(new NafathVerification
        {
            UserId = userId,
            RequestId = requestId,
            RandomNumber = started.RandomNumber,
            Status = NafathStatus.Pending,
            ExpiresAt = now.AddSeconds(ExpirySeconds)
        });
        await db.SaveChangesAsync(ct);

        return new NafathStartDto(requestId, started.RandomNumber, ExpirySeconds);
    }
}

public sealed record GetNafathStatusQuery(string RequestId) : IRequest<Result<NafathStatusDto>>;

public sealed class GetNafathStatusValidator : AbstractValidator<GetNafathStatusQuery>
{
    public GetNafathStatusValidator() => RuleFor(x => x.RequestId).NotEmpty().WithErrorCode("REQUIRED");
}

public sealed class GetNafathStatusHandler(
    IApplicationDbContext db, ICurrentUser currentUser, INafathClient nafath, IFieldProtector protector, IClock clock)
    : IRequestHandler<GetNafathStatusQuery, Result<NafathStatusDto>>
{
    public async Task<Result<NafathStatusDto>> Handle(GetNafathStatusQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var verification = await db.NafathVerifications
            .FirstOrDefaultAsync(n => n.RequestId == r.RequestId && n.UserId == userId, ct);
        if (verification is null) return Error.NotFound("NAFATH_REQUEST_NOT_FOUND");

        if (verification.Status != NafathStatus.Pending)
            return new NafathStatusDto(verification.Status);

        var now = clock.UtcNow;
        if (verification.ExpiresAt <= now)
        {
            verification.Status = NafathStatus.Expired;
            await db.SaveChangesAsync(ct);
            return new NafathStatusDto(NafathStatus.Expired);
        }

        var polled = await nafath.PollAsync(verification.RequestId, verification.CreatedAt, ct);
        if (polled.Status == NafathStatus.Pending) return new NafathStatusDto(NafathStatus.Pending);

        verification.Status = polled.Status;
        verification.CompletedAt = now;

        if (polled.Status == NafathStatus.Verified)
        {
            if (polled.NationalId is not null) verification.NationalIdHash = protector.Hash(polled.NationalId);
            verification.VerifiedName = polled.FullName;

            var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
            user.MarkNafathVerified();
        }

        await db.SaveChangesAsync(ct);
        return new NafathStatusDto(polled.Status);
    }
}
