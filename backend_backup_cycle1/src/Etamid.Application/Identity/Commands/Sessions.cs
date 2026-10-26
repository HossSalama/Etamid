using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Services;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

// ---------- Refresh
public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResultDto>>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() => RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("REQUIRED");
}

public sealed class RefreshTokenHandler(SessionService sessions, UserProfileService profiles)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResultDto>>
{
    public async Task<Result<AuthResultDto>> Handle(RefreshTokenCommand r, CancellationToken ct)
    {
        var rotated = await sessions.RotateAsync(r.RefreshToken, ct);
        if (rotated.IsFailure) return rotated.Error;
        return await profiles.ToAuthResultAsync(rotated.Value, ct);
    }
}

// ---------- Logout (this device)
public sealed record LogoutCommand(string? RefreshToken, string? DeviceToken) : IRequest<Result>;

public sealed class LogoutHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    SessionService sessions,
    IClock clock,
    AuthAuditService audit) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        if (currentUser.SessionId is { } sessionId)
            await sessions.RevokeFamilyAsync(userId, sessionId, RefreshRevokeReason.Logout, ct);

        if (!string.IsNullOrWhiteSpace(r.RefreshToken))
        {
            var hash = TokenHasher.Hash(r.RefreshToken);
            var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
            token?.Revoke(clock.UtcNow, RefreshRevokeReason.Logout);
        }

        if (!string.IsNullOrWhiteSpace(r.DeviceToken))
        {
            var device = await db.Devices.FirstOrDefaultAsync(d => d.DeviceToken == r.DeviceToken && d.UserId == userId, ct);
            if (device is not null) device.IsActive = false;
        }

        audit.Record(AuthEvent.Logout, userId, null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------- Logout from every device
public sealed record LogoutAllCommand : IRequest<Result>;

public sealed class LogoutAllHandler(ICurrentUser currentUser, SessionService sessions, IApplicationDbContext db, AuthAuditService audit)
    : IRequestHandler<LogoutAllCommand, Result>
{
    public async Task<Result> Handle(LogoutAllCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        await sessions.RevokeAllAsync(userId, RefreshRevokeReason.LogoutAll, null, ct);
        audit.Record(AuthEvent.LogoutAll, userId, null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
