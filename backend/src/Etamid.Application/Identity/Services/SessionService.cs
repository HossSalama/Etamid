using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Errors;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Services;

public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresInSeconds, User User);

/// <summary>Login sessions: one refresh-token "family" per sign-in, rotated on every refresh.</summary>
public sealed class SessionService(
    IApplicationDbContext db,
    IAccessTokenGenerator accessTokens,
    IClock clock,
    IRequestContext request,
    AuthAuditService audit)
{
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task<TokenPair> StartSessionAsync(User user, CancellationToken ct)
    {
        var raw = TokenHasher.NewToken();
        var familyId = Guid.NewGuid();

        db.RefreshTokens.Add(new RefreshToken(
            user.Id, TokenHasher.Hash(raw), familyId, clock.UtcNow.Add(RefreshLifetime), null,
            request.IpAddress, request.UserAgent));
        await db.SaveChangesAsync(ct);

        var access = accessTokens.Generate(user, familyId);
        return new TokenPair(access.Token, raw, access.ExpiresInSeconds, user);
    }

    public async Task<Result<TokenPair>> RotateAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenHasher.Hash(refreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return IdentityErrors.TokenInvalid;

        var now = clock.UtcNow;

        if (token.RevokedAt is not null)
        {
            // A rotated token showing up again means it was copied: kill every session of this user.
            if (token.RevokedReason == RefreshRevokeReason.Rotated)
            {
                audit.Record(AuthEvent.TokenReuseDetected, token.UserId, null);
                await RevokeAllAsync(token.UserId, RefreshRevokeReason.ReuseDetected, null, ct);
                return IdentityErrors.RefreshReused;
            }
            return IdentityErrors.TokenInvalid;
        }

        if (token.ExpiresAt <= now) return IdentityErrors.TokenInvalid;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId && u.DeletedAt == null, ct);
        if (user is null || user.Status != AccountStatus.Active) return IdentityErrors.TokenInvalid;

        var raw = TokenHasher.NewToken();
        var next = new RefreshToken(
            user.Id, TokenHasher.Hash(raw), token.FamilyId, now.Add(RefreshLifetime), token.DeviceId,
            request.IpAddress, request.UserAgent);

        token.Revoke(now, RefreshRevokeReason.Rotated);
        db.RefreshTokens.Add(next);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two refreshes raced with the same token; only one may win.
            return IdentityErrors.TokenInvalid;
        }

        token.SetReplacedBy(next.Id);
        await db.SaveChangesAsync(ct);

        var access = accessTokens.Generate(user, token.FamilyId);
        return new TokenPair(access.Token, raw, access.ExpiresInSeconds, user);
    }

    public async Task RevokeFamilyAsync(long userId, Guid familyId, RefreshRevokeReason reason, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(now, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(long userId, RefreshRevokeReason reason, Guid? exceptFamily, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens.Where(t => exceptFamily is null || t.FamilyId != exceptFamily))
            t.Revoke(now, reason);
        await db.SaveChangesAsync(ct);
    }
}
