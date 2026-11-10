using System.Text.Json;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Services;
using Etamid.Domain.Admin;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.AdminAuth;

/// <summary>Adds AuditLog rows. Callers save the context, so the row commits with their change.</summary>
public sealed class AdminAuditService(IApplicationDbContext db, IRequestContext request)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Record(
        AuditActorType actorType, long? actorId, string action, string entityType, long entityId,
        object? oldValues = null, object? newValues = null)
        => db.AuditLogs.Add(new AuditLog
        {
            ActorType = actorType,
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, Json),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, Json),
            Ip = request.IpAddress
        });
}

public sealed record AdminTokenPair(string AccessToken, string RefreshToken, int ExpiresInSeconds, AdminUser Admin);

/// <summary>Admin login sessions: one refresh-token family per sign-in, rotated on every refresh. Own table, never RefreshTokens.</summary>
public sealed class AdminSessionService(
    IApplicationDbContext db,
    IAdminAccessTokenGenerator accessTokens,
    IClock clock,
    IRequestContext request,
    AdminAuditService audit)
{
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task<AdminTokenPair> StartSessionAsync(AdminUser admin, CancellationToken ct)
    {
        var raw = TokenHasher.NewToken();
        var familyId = Guid.NewGuid();

        db.AdminRefreshTokens.Add(new AdminRefreshToken(
            admin.Id, TokenHasher.Hash(raw), familyId, clock.UtcNow.Add(RefreshLifetime),
            request.IpAddress, request.UserAgent));
        await db.SaveChangesAsync(ct);

        var access = accessTokens.Generate(admin, familyId);
        return new AdminTokenPair(access.Token, raw, access.ExpiresInSeconds, admin);
    }

    public async Task<Result<AdminTokenPair>> RotateAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenHasher.Hash(refreshToken);
        var token = await db.AdminRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return AdminErrors.TokenInvalid;

        var now = clock.UtcNow;

        if (token.RevokedAt is not null)
        {
            // A rotated token showing up again means it was copied: kill every admin session of this admin.
            if (token.RevokedReason == RefreshRevokeReason.Rotated)
            {
                audit.Record(AuditActorType.Admin, token.AdminUserId, "admin.refresh.reuse_detected", "AdminUser", token.AdminUserId);
                await RevokeAllAsync(token.AdminUserId, RefreshRevokeReason.ReuseDetected, null, ct);
                return AdminErrors.RefreshReused;
            }
            return AdminErrors.TokenInvalid;
        }

        if (token.ExpiresAt <= now) return AdminErrors.TokenInvalid;

        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == token.AdminUserId, ct);
        if (admin is null || admin.Status != AdminStatus.Active) return AdminErrors.TokenInvalid;

        var raw = TokenHasher.NewToken();
        var next = new AdminRefreshToken(
            admin.Id, TokenHasher.Hash(raw), token.FamilyId, now.Add(RefreshLifetime),
            request.IpAddress, request.UserAgent);

        token.Revoke(now, RefreshRevokeReason.Rotated);
        db.AdminRefreshTokens.Add(next);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two refreshes raced with the same token; only one may win.
            return AdminErrors.TokenInvalid;
        }

        token.SetReplacedBy(next.Id);
        await db.SaveChangesAsync(ct);

        var access = accessTokens.Generate(admin, token.FamilyId);
        return new AdminTokenPair(access.Token, raw, access.ExpiresInSeconds, admin);
    }

    public async Task RevokeFamilyAsync(long adminId, Guid familyId, RefreshRevokeReason reason, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tokens = await db.AdminRefreshTokens
            .Where(t => t.AdminUserId == adminId && t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.Revoke(now, reason);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllAsync(long adminId, RefreshRevokeReason reason, Guid? exceptFamily, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var tokens = await db.AdminRefreshTokens
            .Where(t => t.AdminUserId == adminId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens.Where(t => exceptFamily is null || t.FamilyId != exceptFamily))
            t.Revoke(now, reason);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Builds the admin object returned by auth and management endpoints (roles and permissions come from the database).</summary>
public sealed class AdminProfileService(IApplicationDbContext db)
{
    public async Task<AdminDto> BuildAsync(AdminUser admin, CancellationToken ct)
    {
        var roleIds = await db.AdminUserRoles.AsNoTracking()
            .Where(x => x.AdminUserId == admin.Id).Select(x => x.RoleId).ToListAsync(ct);

        var roles = roleIds.Count == 0
            ? new List<string>()
            : await db.AdminRoles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).Select(x => x.Name).OrderBy(x => x).ToListAsync(ct);

        var permissionIds = roleIds.Count == 0
            ? new List<long>()
            : await db.AdminRolePermissions.AsNoTracking()
                .Where(x => roleIds.Contains(x.RoleId)).Select(x => x.PermissionId).Distinct().ToListAsync(ct);

        var permissions = permissionIds.Count == 0
            ? new List<string>()
            : await db.AdminPermissions.AsNoTracking()
                .Where(x => permissionIds.Contains(x.Id)).Select(x => x.Code).OrderBy(x => x).ToListAsync(ct);

        return new AdminDto(admin.Id, admin.Name, admin.Email, admin.Status, admin.TwoFactorEnabled, admin.LastLoginAt, roles, permissions);
    }

    public async Task<AdminAuthResultDto> ToAuthResultAsync(AdminTokenPair pair, CancellationToken ct)
        => new(pair.AccessToken, pair.RefreshToken, pair.ExpiresInSeconds, await BuildAsync(pair.Admin, ct));
}

/// <summary>
/// Per-request authorization, always from the database
/// (AdminUserRole -> AdminRolePermission -> AdminPermission); the JWT carries no permissions.
/// Also rejects tokens of disabled admins and of revoked sessions.
/// </summary>
public sealed class AdminAccessService(IApplicationDbContext db, ICurrentAdmin current, IClock clock)
{
    public async Task<Result<AdminUser>> RequireAsync(string? permission, bool superAdminOnly, CancellationToken ct)
    {
        if (current.AdminId is not { } adminId) return CommonErrors.Unauthorized;

        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == adminId, ct);
        if (admin is null || admin.Status != AdminStatus.Active) return AdminErrors.TokenInvalid;

        if (current.SessionId is not { } sessionId) return AdminErrors.TokenInvalid;
        var now = clock.UtcNow;
        var alive = await db.AdminRefreshTokens.AnyAsync(
            t => t.AdminUserId == adminId && t.FamilyId == sessionId && t.RevokedAt == null && t.ExpiresAt > now, ct);
        if (!alive) return AdminErrors.TokenInvalid;

        if (superAdminOnly && !await IsSuperAdminAsync(adminId, ct)) return AdminErrors.SuperAdminRequired;
        if (permission is not null && !await HasPermissionAsync(adminId, permission, ct)) return AdminErrors.Forbidden;

        return admin;
    }

    /// <summary>
    /// Gate for the operator endpoints under /api/v1/admin. Those routes authenticate with the app scheme AND the
    /// AdminJwt scheme so that an app-user token reaches here and is refused with 403 (a missing token is already 401).
    /// A caller that is authenticated but is not an admin never gets through.
    /// </summary>
    public async Task<Result<AdminUser>> RequireOperatorAsync(string permission, CancellationToken ct)
    {
        if (current.AdminId is null) return AdminErrors.Forbidden;
        return await RequireAsync(permission, false, ct);
    }

    public async Task<bool> IsSuperAdminAsync(long adminId, CancellationToken ct)
        => await (from ur in db.AdminUserRoles
                  join role in db.AdminRoles on ur.RoleId equals role.Id
                  where ur.AdminUserId == adminId && role.Name == AdminRoleNames.SuperAdmin
                  select ur.RoleId).AnyAsync(ct);

    public async Task<bool> HasPermissionAsync(long adminId, string permission, CancellationToken ct)
        => await (from ur in db.AdminUserRoles
                  join rp in db.AdminRolePermissions on ur.RoleId equals rp.RoleId
                  join p in db.AdminPermissions on rp.PermissionId equals p.Id
                  where ur.AdminUserId == adminId && p.Code == permission
                  select p.Id).AnyAsync(ct);

    /// <summary>Active super-admins other than <paramref name="adminId"/>.</summary>
    public async Task<int> CountOtherActiveSuperAdminsAsync(long adminId, CancellationToken ct)
        => await (from ur in db.AdminUserRoles
                  join role in db.AdminRoles on ur.RoleId equals role.Id
                  join a in db.AdminUsers on ur.AdminUserId equals a.Id
                  where role.Name == AdminRoleNames.SuperAdmin && a.Status == AdminStatus.Active && a.Id != adminId
                  select a.Id).Distinct().CountAsync(ct);
}
