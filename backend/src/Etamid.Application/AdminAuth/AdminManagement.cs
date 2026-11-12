using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Admin;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.AdminAuth;

// Reading admins needs admin.roles.manage. Every admin-account WRITE is super-admin only
// (role membership checked in the database) AND needs admin.roles.manage.

// ======================================================================= List
public sealed record ListAdminsQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<AdminListItemDto>>>;

public sealed class ListAdminsHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListAdminsQuery, Result<PagedResult<AdminListItemDto>>>
{
    public async Task<Result<PagedResult<AdminListItemDto>>> Handle(ListAdminsQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, false, ct);
        if (guard.IsFailure) return guard.Error;

        AdminStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<AdminStatus>(query.Status, ignoreCase: true, out var parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);
        var source = db.AdminUsers.AsNoTracking();
        if (status is { } s) source = source.Where(a => a.Status == s);

        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var ids = items.Select(a => a.Id).ToList();
        var links = await (from ur in db.AdminUserRoles
                           join role in db.AdminRoles on ur.RoleId equals role.Id
                           where ids.Contains(ur.AdminUserId)
                           select new { ur.AdminUserId, role.Name }).ToListAsync(ct);

        var dtos = items.Select(a => new AdminListItemDto(
            a.Id, a.Name, a.Email, a.Status, a.TwoFactorEnabled, a.LastLoginAt, a.CreatedAt,
            links.Where(l => l.AdminUserId == a.Id).Select(l => l.Name).OrderBy(n => n).ToList())).ToList();
        return new PagedResult<AdminListItemDto>(dtos, page, pageSize, total);
    }
}

public sealed record GetAdminQuery(long Id) : IRequest<Result<AdminDto>>;

public sealed class GetAdminHandler(IApplicationDbContext db, AdminAccessService access, AdminProfileService profiles)
    : IRequestHandler<GetAdminQuery, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(GetAdminQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, false, ct);
        if (guard.IsFailure) return guard.Error;

        var admin = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == query.Id, ct);
        if (admin is null) return AdminErrors.NotFound;
        return await profiles.BuildAsync(admin, ct);
    }
}

// ======================================================================= Create
public sealed record CreateAdminCommand(string Name, string Email, string Password, IReadOnlyList<long>? RoleIds)
    : IRequest<Result<AdminDto>>;

public sealed class CreateAdminValidator : AbstractValidator<CreateAdminCommand>
{
    public CreateAdminValidator()
    {
        RuleFor(x => x.Name).MustBeRequired(150);
        RuleFor(x => x.Email).MustBeEmail();
        RuleFor(x => x.Password).NotEmpty().WithErrorCode("REQUIRED")
            .Must(AdminPasswordPolicy.IsValid).WithErrorCode("ADMIN_WEAK_PASSWORD");
    }
}

public sealed class CreateAdminHandler(
    IApplicationDbContext db, AdminAccessService access, IPasswordHasher hasher,
    AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<CreateAdminCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(CreateAdminCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var email = cmd.Email.Trim().ToLowerInvariant();
        if (await db.AdminUsers.AnyAsync(a => a.Email == email, ct)) return AdminErrors.EmailExists;

        var roleIds = (cmd.RoleIds ?? Array.Empty<long>()).Distinct().ToList();
        if (roleIds.Count > 0 && await db.AdminRoles.CountAsync(x => roleIds.Contains(x.Id), ct) != roleIds.Count)
            return AdminErrors.RoleNotFound;

        var admin = new AdminUser
        {
            Name = cmd.Name.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(cmd.Password),
            Status = AdminStatus.Active
        };
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync(ct);

        foreach (var roleId in roleIds)
            db.AdminUserRoles.Add(new AdminUserRole { AdminUserId = admin.Id, RoleId = roleId });
        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.create", "AdminUser", admin.Id, null,
            new { admin.Name, admin.Email, roleIds });
        await db.SaveChangesAsync(ct);

        return await profiles.BuildAsync(admin, ct);
    }
}

// ======================================================================= Update (PATCH)
public sealed record UpdateAdminCommand(long Id, string? Name, string? Email) : IRequest<Result<AdminDto>>;

public sealed class UpdateAdminValidator : AbstractValidator<UpdateAdminCommand>
{
    public UpdateAdminValidator()
    {
        RuleFor(x => x.Name!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Name is not null);
        RuleFor(x => x.Email!).MustBeEmail().When(x => x.Email is not null);
    }
}

public sealed class UpdateAdminHandler(
    IApplicationDbContext db, AdminAccessService access, AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<UpdateAdminCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(UpdateAdminCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var target = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct);
        if (target is null) return AdminErrors.NotFound;

        var before = new { target.Name, target.Email };
        if (!string.IsNullOrWhiteSpace(cmd.Name)) target.Name = cmd.Name.Trim();
        if (!string.IsNullOrWhiteSpace(cmd.Email))
        {
            var email = cmd.Email.Trim().ToLowerInvariant();
            if (await db.AdminUsers.AnyAsync(a => a.Email == email && a.Id != target.Id, ct)) return AdminErrors.EmailExists;
            target.Email = email;
        }

        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.update", "AdminUser", target.Id, before, new { target.Name, target.Email });
        await db.SaveChangesAsync(ct);
        return await profiles.BuildAsync(target, ct);
    }
}

// ======================================================================= Disable / enable
public sealed record DisableAdminCommand(long Id) : IRequest<Result<AdminDto>>;

public sealed class DisableAdminHandler(
    IApplicationDbContext db, AdminAccessService access, AdminSessionService sessions,
    AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<DisableAdminCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(DisableAdminCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;
        var actor = guard.Value;

        var target = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct);
        if (target is null) return AdminErrors.NotFound;
        if (target.Id == actor.Id) return AdminErrors.CannotDisableSelf;

        if (target.Status == AdminStatus.Active
            && await access.IsSuperAdminAsync(target.Id, ct)
            && await access.CountOtherActiveSuperAdminsAsync(target.Id, ct) == 0)
            return AdminErrors.LastSuperAdmin;

        var before = new { status = target.Status.ToString() };
        target.Status = AdminStatus.Disabled;
        audit.Record(AuditActorType.Admin, actor.Id, "admin.disable", "AdminUser", target.Id, before, new { status = "Disabled" });
        await sessions.RevokeAllAsync(target.Id, RefreshRevokeReason.LogoutAll, null, ct);   // saves everything above
        return await profiles.BuildAsync(target, ct);
    }
}

public sealed record EnableAdminCommand(long Id) : IRequest<Result<AdminDto>>;

public sealed class EnableAdminHandler(
    IApplicationDbContext db, AdminAccessService access, AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<EnableAdminCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(EnableAdminCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var target = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct);
        if (target is null) return AdminErrors.NotFound;

        var before = new { status = target.Status.ToString() };
        target.Status = AdminStatus.Active;
        target.FailedLoginCount = 0;
        target.LockedUntil = null;
        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.enable", "AdminUser", target.Id, before, new { status = "Active" });
        await db.SaveChangesAsync(ct);
        return await profiles.BuildAsync(target, ct);
    }
}

// ======================================================================= Reset 2FA
public sealed record ResetAdminTwoFactorCommand(long Id) : IRequest<Result<AdminDto>>;

public sealed class ResetAdminTwoFactorHandler(
    IApplicationDbContext db, AdminAccessService access, AdminSessionService sessions,
    AdminProfileService profiles, AdminAuditService audit, IClock clock)
    : IRequestHandler<ResetAdminTwoFactorCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(ResetAdminTwoFactorCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var target = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == cmd.Id, ct);
        if (target is null) return AdminErrors.NotFound;

        var before = new { twoFactorEnabled = target.TwoFactorEnabled };
        target.TwoFactorEnabled = false;
        target.TwoFactorSecret = null;

        var now = clock.UtcNow;
        var open = await db.AdminTwoFactorChallenges.Where(c => c.AdminUserId == target.Id && c.ConsumedAt == null).ToListAsync(ct);
        foreach (var challenge in open) challenge.ConsumedAt = now;

        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.2fa.reset", "AdminUser", target.Id, before, new { twoFactorEnabled = false });
        // The target's sessions are revoked too: the reset is usually a lost-device or suspected-compromise case.
        await sessions.RevokeAllAsync(target.Id, RefreshRevokeReason.LogoutAll, null, ct);
        return await profiles.BuildAsync(target, ct);
    }
}

// ======================================================================= Assign roles (replaces the whole set)
public sealed record SetAdminRolesCommand(long AdminId, IReadOnlyList<long>? RoleIds) : IRequest<Result<AdminDto>>;

public sealed class SetAdminRolesHandler(
    IApplicationDbContext db, AdminAccessService access, AdminProfileService profiles, AdminAuditService audit)
    : IRequestHandler<SetAdminRolesCommand, Result<AdminDto>>
{
    public async Task<Result<AdminDto>> Handle(SetAdminRolesCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var target = await db.AdminUsers.FirstOrDefaultAsync(a => a.Id == cmd.AdminId, ct);
        if (target is null) return AdminErrors.NotFound;

        var wanted = (cmd.RoleIds ?? Array.Empty<long>()).Distinct().ToList();
        if (wanted.Count > 0 && await db.AdminRoles.CountAsync(x => wanted.Contains(x.Id), ct) != wanted.Count)
            return AdminErrors.RoleNotFound;

        var current = await db.AdminUserRoles.Where(x => x.AdminUserId == target.Id).ToListAsync(ct);
        var currentIds = current.Select(c => c.RoleId).ToList();

        var superRoleId = await db.AdminRoles.Where(x => x.Name == AdminRoleNames.SuperAdmin).Select(x => x.Id).FirstAsync(ct);
        if (currentIds.Contains(superRoleId) && !wanted.Contains(superRoleId) && target.Status == AdminStatus.Active
            && await access.CountOtherActiveSuperAdminsAsync(target.Id, ct) == 0)
            return AdminErrors.LastSuperAdmin;

        db.AdminUserRoles.RemoveRange(current.Where(c => !wanted.Contains(c.RoleId)));
        foreach (var roleId in wanted.Where(id => !currentIds.Contains(id)))
            db.AdminUserRoles.Add(new AdminUserRole { AdminUserId = target.Id, RoleId = roleId });

        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.roles.assign", "AdminUser", target.Id,
            new { roleIds = currentIds }, new { roleIds = wanted });
        await db.SaveChangesAsync(ct);
        return await profiles.BuildAsync(target, ct);
    }
}
