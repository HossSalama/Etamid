using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Identity.Errors;
using Etamid.Domain.Admin;
using Etamid.Domain.Deals;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.AdminAuth;

// Admin users (Cycle 9, task 4): one list for companies AND individuals (?type=), detail, soft delete.
// User does not implement ISoftDeletable, so NO global filter hides deleted users: every query below
// excludes Status == Deleted explicitly.

public sealed record AdminUserListItemDto(
    long Id, string Code, UserType Type, string Name, string Phone, string Email,
    AccountStatus Status, bool NafathVerified, DateTime CreatedAt);

public sealed record AdminCompanyProfileDto(string CompanyName, string CommercialRegNo, string? TaxNo, string OwnerPhone);

public sealed record AdminUserDetailDto(
    long Id, string Code, UserType Type, string Name, string Phone, string Email,
    AccountStatus Status, bool NafathVerified, DateTime CreatedAt,
    Language Language, DateTime? PhoneVerifiedAt, DateTime? EmailVerifiedAt, DateTime? LastLoginAt,
    AdminCompanyProfileDto? CompanyProfile, int DealsAsBuyer, int DealsAsSeller);

public static class AdminUserErrors
{
    public static readonly Error HasActiveDeals = Error.Conflict("USER_HAS_ACTIVE_DEALS");
}

// ======================================================================= List (admin.users.view)
public sealed record ListAdminUsersQuery(string? Type, string? Status, string? Search, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<AdminUserListItemDto>>>;

public sealed class ListAdminUsersHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListAdminUsersQuery, Result<PagedResult<AdminUserListItemDto>>>
{
    public async Task<Result<PagedResult<AdminUserListItemDto>>> Handle(ListAdminUsersQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.UsersView, false, ct);
        if (guard.IsFailure) return guard.Error;

        // type: exactly "individual" or "company" (case-insensitive). Numbers and anything else are refused.
        UserType? type = null;
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            switch (query.Type.Trim().ToLowerInvariant())
            {
                case "individual": type = UserType.Individual; break;
                case "company": type = UserType.Company; break;
                default: return Error.Validation("INVALID_VALUE", "type");
            }
        }

        // status: AccountStatus by name, snake_case or PascalCase ("pending_verification"); numbers are refused.
        AccountStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normalized = query.Status.Trim().Replace("_", string.Empty);
            if (normalized.All(char.IsDigit)
                || !Enum.TryParse<AccountStatus>(normalized, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var source = db.Users.AsNoTracking().Where(u => u.Status != AccountStatus.Deleted);
        if (type is { } t) source = source.Where(u => u.Type == t);
        if (status is { } s) source = source.Where(u => u.Status == s);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            source = source.Where(u => u.Name.ToLower().Contains(term)
                                       || u.Email.ToLower().Contains(term)
                                       || u.Phone.ToLower().Contains(term));
        }

        var total = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new AdminUserListItemDto(
                u.Id, u.Code, u.Type, u.Name, u.Phone, u.Email, u.Status, u.NafathVerified, u.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<AdminUserListItemDto>(items, page, pageSize, total);
    }
}

// ======================================================================= Detail (admin.users.view)
public sealed record GetAdminUserQuery(long Id) : IRequest<Result<AdminUserDetailDto>>;

public sealed class GetAdminUserHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<GetAdminUserQuery, Result<AdminUserDetailDto>>
{
    public async Task<Result<AdminUserDetailDto>> Handle(GetAdminUserQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.UsersView, false, ct);
        if (guard.IsFailure) return guard.Error;

        var user = await db.Users.AsNoTracking().Include(u => u.CompanyProfile)
            .FirstOrDefaultAsync(u => u.Id == query.Id && u.Status != AccountStatus.Deleted, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        var dealsAsBuyer = await db.Deals.AsNoTracking().CountAsync(d => d.BuyerId == user.Id, ct);
        var dealsAsSeller = await db.Deals.AsNoTracking().CountAsync(d => d.SellerId == user.Id, ct);

        var company = user.CompanyProfile is null
            ? null
            : new AdminCompanyProfileDto(
                user.CompanyProfile.CompanyName, user.CompanyProfile.CommercialRegNo,
                user.CompanyProfile.TaxNo, user.CompanyProfile.OwnerPhone);

        return new AdminUserDetailDto(
            user.Id, user.Code, user.Type, user.Name, user.Phone, user.Email, user.Status, user.NafathVerified, user.CreatedAt,
            user.Language, user.PhoneVerifiedAt, user.EmailVerifiedAt, user.LastLoginAt,
            company, dealsAsBuyer, dealsAsSeller);
    }
}

// ======================================================================= Delete (admin.users.manage) - soft delete only
public sealed record DeleteAdminUserCommand(long Id) : IRequest<Result>;

/// <summary>
/// Soft delete: Status = Deleted + DeletedAt (User.MarkDeleted). Refused while the user is buyer or seller in a deal that is not
/// finished. The user row, CompanyProfile, wallet and every deal stay. Active app sessions (refresh tokens) are revoked.
/// </summary>
public sealed class DeleteAdminUserHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IClock clock)
    : IRequestHandler<DeleteAdminUserCommand, Result>
{
    private static readonly DealStatus[] FinishedDealStatuses =
        [DealStatus.Released, DealStatus.Rejected, DealStatus.Cancelled, DealStatus.Expired, DealStatus.Refunded];

    public async Task<Result> Handle(DeleteAdminUserCommand cmd, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.UsersManage, false, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == cmd.Id && u.Status != AccountStatus.Deleted, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        var hasActiveDeal = await db.Deals.AsNoTracking().AnyAsync(
            d => (d.BuyerId == user.Id || d.SellerId == user.Id) && !FinishedDealStatuses.Contains(d.Status), ct);
        if (hasActiveDeal) return AdminUserErrors.HasActiveDeals;

        var now = clock.UtcNow;
        var before = user.Status;
        user.MarkDeleted(now);

        var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null).ToListAsync(ct);
        foreach (var token in tokens) token.Revoke(now, RefreshRevokeReason.LogoutAll);

        audit.Record(AuditActorType.Admin, admin.Id, "admin.user.delete", "User", user.Id,
            new { status = before.ToString() }, new { status = AccountStatus.Deleted.ToString() });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
