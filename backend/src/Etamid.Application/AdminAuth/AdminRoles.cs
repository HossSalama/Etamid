using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Admin;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.AdminAuth;

internal static class AdminRoleMapper
{
    public static async Task<IReadOnlyList<AdminRoleDto>> BuildAsync(
        IApplicationDbContext db, IReadOnlyList<AdminRole> roles, CancellationToken ct)
    {
        var ids = roles.Select(r => r.Id).ToList();
        var links = await (from rp in db.AdminRolePermissions.AsNoTracking()
                           join p in db.AdminPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                           where ids.Contains(rp.RoleId)
                           select new { rp.RoleId, p.Code }).ToListAsync(ct);

        return roles.Select(role => new AdminRoleDto(
            role.Id, role.Name, role.Description,
            links.Where(l => l.RoleId == role.Id).Select(l => l.Code).OrderBy(c => c).ToList())).ToList();
    }
}

// ======================================================================= Permissions (read)
public sealed record ListAdminPermissionsQuery : IRequest<Result<IReadOnlyList<AdminPermissionDto>>>;

public sealed class ListAdminPermissionsHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListAdminPermissionsQuery, Result<IReadOnlyList<AdminPermissionDto>>>
{
    public async Task<Result<IReadOnlyList<AdminPermissionDto>>> Handle(ListAdminPermissionsQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, false, ct);
        if (guard.IsFailure) return guard.Error;

        var rows = await db.AdminPermissions.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
        IReadOnlyList<AdminPermissionDto> dtos = rows.Select(p => new AdminPermissionDto(p.Id, p.Code, p.Description)).ToList();
        return Result.Success(dtos);
    }
}

// ======================================================================= Roles (read)
public sealed record ListAdminRolesQuery : IRequest<Result<IReadOnlyList<AdminRoleDto>>>;

public sealed class ListAdminRolesHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListAdminRolesQuery, Result<IReadOnlyList<AdminRoleDto>>>
{
    public async Task<Result<IReadOnlyList<AdminRoleDto>>> Handle(ListAdminRolesQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, false, ct);
        if (guard.IsFailure) return guard.Error;

        var roles = await db.AdminRoles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(ct);
        return Result.Success(await AdminRoleMapper.BuildAsync(db, roles, ct));
    }
}

public sealed record GetAdminRoleQuery(long Id) : IRequest<Result<AdminRoleDto>>;

public sealed class GetAdminRoleHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<GetAdminRoleQuery, Result<AdminRoleDto>>
{
    public async Task<Result<AdminRoleDto>> Handle(GetAdminRoleQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, false, ct);
        if (guard.IsFailure) return guard.Error;

        var role = await db.AdminRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == query.Id, ct);
        if (role is null) return AdminErrors.RoleNotFound;
        return (await AdminRoleMapper.BuildAsync(db, [role], ct))[0];
    }
}

// ======================================================================= Roles (write: super-admin only)
public sealed record CreateAdminRoleCommand(string Name, string? Description) : IRequest<Result<AdminRoleDto>>;

public sealed class CreateAdminRoleValidator : AbstractValidator<CreateAdminRoleCommand>
{
    public CreateAdminRoleValidator()
    {
        RuleFor(x => x.Name).MustBeRequired(100);
        RuleFor(x => x.Description!).MaximumLength(300).WithErrorCode("TOO_LONG").When(x => x.Description is not null);
    }
}

public sealed class CreateAdminRoleHandler(IApplicationDbContext db, AdminAccessService access, AdminAuditService audit)
    : IRequestHandler<CreateAdminRoleCommand, Result<AdminRoleDto>>
{
    public async Task<Result<AdminRoleDto>> Handle(CreateAdminRoleCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var name = cmd.Name.Trim();
        if (await db.AdminRoles.AnyAsync(r => r.Name == name, ct)) return AdminErrors.RoleNameExists;

        var role = new AdminRole { Name = name, Description = string.IsNullOrWhiteSpace(cmd.Description) ? null : cmd.Description.Trim() };
        db.AdminRoles.Add(role);
        await db.SaveChangesAsync(ct);

        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.role.create", "AdminRole", role.Id, null, new { role.Name, role.Description });
        await db.SaveChangesAsync(ct);
        return new AdminRoleDto(role.Id, role.Name, role.Description, []);
    }
}

public sealed record UpdateAdminRoleCommand(long Id, string? Name, string? Description) : IRequest<Result<AdminRoleDto>>;

public sealed class UpdateAdminRoleValidator : AbstractValidator<UpdateAdminRoleCommand>
{
    public UpdateAdminRoleValidator()
    {
        RuleFor(x => x.Name!).MaximumLength(100).WithErrorCode("TOO_LONG").When(x => x.Name is not null);
        RuleFor(x => x.Description!).MaximumLength(300).WithErrorCode("TOO_LONG").When(x => x.Description is not null);
    }
}

public sealed class UpdateAdminRoleHandler(IApplicationDbContext db, AdminAccessService access, AdminAuditService audit)
    : IRequestHandler<UpdateAdminRoleCommand, Result<AdminRoleDto>>
{
    public async Task<Result<AdminRoleDto>> Handle(UpdateAdminRoleCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var role = await db.AdminRoles.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);
        if (role is null) return AdminErrors.RoleNotFound;
        if (role.Name == AdminRoleNames.SuperAdmin) return AdminErrors.RoleProtected;

        var before = new { role.Name, role.Description };
        if (!string.IsNullOrWhiteSpace(cmd.Name))
        {
            var name = cmd.Name.Trim();
            if (name == AdminRoleNames.SuperAdmin || await db.AdminRoles.AnyAsync(r => r.Name == name && r.Id != role.Id, ct))
                return AdminErrors.RoleNameExists;
            role.Name = name;
        }
        if (cmd.Description is not null) role.Description = string.IsNullOrWhiteSpace(cmd.Description) ? null : cmd.Description.Trim();

        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.role.update", "AdminRole", role.Id, before, new { role.Name, role.Description });
        await db.SaveChangesAsync(ct);
        return (await AdminRoleMapper.BuildAsync(db, [role], ct))[0];
    }
}

public sealed record DeleteAdminRoleCommand(long Id) : IRequest<Result>;

public sealed class DeleteAdminRoleHandler(IApplicationDbContext db, AdminAccessService access, AdminAuditService audit)
    : IRequestHandler<DeleteAdminRoleCommand, Result>
{
    public async Task<Result> Handle(DeleteAdminRoleCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var role = await db.AdminRoles.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);
        if (role is null) return AdminErrors.RoleNotFound;
        if (role.Name == AdminRoleNames.SuperAdmin) return AdminErrors.RoleProtected;
        if (await db.AdminUserRoles.AnyAsync(x => x.RoleId == role.Id, ct)) return AdminErrors.RoleInUse;

        var links = await db.AdminRolePermissions.Where(x => x.RoleId == role.Id).ToListAsync(ct);
        db.AdminRolePermissions.RemoveRange(links);
        db.AdminRoles.Remove(role);
        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.role.delete", "AdminRole", role.Id, new { role.Name, role.Description }, null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Replaces the role's whole permission set with the given codes.</summary>
public sealed record SetAdminRolePermissionsCommand(long RoleId, IReadOnlyList<string>? Permissions) : IRequest<Result<AdminRoleDto>>;

public sealed class SetAdminRolePermissionsHandler(IApplicationDbContext db, AdminAccessService access, AdminAuditService audit)
    : IRequestHandler<SetAdminRolePermissionsCommand, Result<AdminRoleDto>>
{
    public async Task<Result<AdminRoleDto>> Handle(SetAdminRolePermissionsCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.RolesManage, true, ct);
        if (guard.IsFailure) return guard.Error;

        var role = await db.AdminRoles.FirstOrDefaultAsync(r => r.Id == cmd.RoleId, ct);
        if (role is null) return AdminErrors.RoleNotFound;
        if (role.Name == AdminRoleNames.SuperAdmin) return AdminErrors.RoleProtected;

        var codes = (cmd.Permissions ?? Array.Empty<string>()).Select(c => c.Trim()).Distinct().ToList();
        var permissions = await db.AdminPermissions.Where(p => codes.Contains(p.Code)).ToListAsync(ct);
        if (permissions.Count != codes.Count) return AdminErrors.PermissionNotFound;

        var current = await db.AdminRolePermissions.Where(x => x.RoleId == role.Id).ToListAsync(ct);
        var currentCodes = await (from rp in db.AdminRolePermissions
                                  join p in db.AdminPermissions on rp.PermissionId equals p.Id
                                  where rp.RoleId == role.Id
                                  select p.Code).ToListAsync(ct);

        var wantedIds = permissions.Select(p => p.Id).ToList();
        db.AdminRolePermissions.RemoveRange(current.Where(c => !wantedIds.Contains(c.PermissionId)));
        var currentIds = current.Select(c => c.PermissionId).ToList();
        foreach (var permissionId in wantedIds.Where(id => !currentIds.Contains(id)))
            db.AdminRolePermissions.Add(new AdminRolePermission { RoleId = role.Id, PermissionId = permissionId });

        audit.Record(AuditActorType.Admin, guard.Value.Id, "admin.role.permissions", "AdminRole", role.Id,
            new { permissions = currentCodes.OrderBy(c => c).ToList() }, new { permissions = codes.OrderBy(c => c).ToList() });
        await db.SaveChangesAsync(ct);
        return (await AdminRoleMapper.BuildAsync(db, [role], ct))[0];
    }
}

// ======================================================================= Audit logs (admin.audit.view)
public sealed record ListAuditLogsQuery(string? Action, string? EntityType, long? ActorId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<AuditLogDto>>>;

public sealed class ListAuditLogsHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListAuditLogsQuery, Result<PagedResult<AuditLogDto>>>
{
    public async Task<Result<PagedResult<AuditLogDto>>> Handle(ListAuditLogsQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.AuditView, false, ct);
        if (guard.IsFailure) return guard.Error;

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var source = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action)) { var action = query.Action.Trim(); source = source.Where(a => a.Action == action); }
        if (!string.IsNullOrWhiteSpace(query.EntityType)) { var entity = query.EntityType.Trim(); source = source.Where(a => a.EntityType == entity); }
        if (query.ActorId is { } actorId) source = source.Where(a => a.ActorId == actorId);

        var total = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // Resolve actor names in two batched lookups
        var adminIds = rows.Where(a => a.ActorType == AuditActorType.Admin && a.ActorId.HasValue)
            .Select(a => a.ActorId!.Value).Distinct().ToList();
        var userIds = rows.Where(a => a.ActorType == AuditActorType.User && a.ActorId.HasValue)
            .Select(a => a.ActorId!.Value).Distinct().ToList();

        var adminNames = await db.AdminUsers.AsNoTracking().Where(u => adminIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var userNames = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        string? ResolveActorName(AuditLog log) => log.ActorType switch
        {
            AuditActorType.Admin when log.ActorId.HasValue => adminNames.GetValueOrDefault(log.ActorId.Value),
            AuditActorType.User when log.ActorId.HasValue => userNames.GetValueOrDefault(log.ActorId.Value),
            _ => null
        };

        var dtos = rows.Select(a => new AuditLogDto(
            a.Id, a.ActorType, a.ActorId, ResolveActorName(a), a.Action, a.EntityType, a.EntityId,
            a.OldValues, a.NewValues, a.Ip, a.CreatedAt)).ToList();
        return new PagedResult<AuditLogDto>(dtos, page, pageSize, total);

    }
}
