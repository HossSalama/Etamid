using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.AdminAuth;

/// <summary>Counts of app users for the admin dashboard (admin.users.view).</summary>
public sealed record AppUserCountsDto(long Sellers, long Buyers, long Companies, long Individuals);

public sealed record GetAppUserCountsQuery : IRequest<Result<AppUserCountsDto>>;

public sealed class GetAppUserCountsHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<GetAppUserCountsQuery, Result<AppUserCountsDto>>
{
    public async Task<Result<AppUserCountsDto>> Handle(GetAppUserCountsQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.UsersView, false, ct);
        if (guard.IsFailure) return guard.Error;

        var sellers = await db.Deals.AsNoTracking().Select(d => d.SellerId).Distinct().CountAsync(ct);
        var buyers = await db.Deals.AsNoTracking().Select(d => d.BuyerId).Distinct().CountAsync(ct);
        var companies = await db.Users.AsNoTracking().CountAsync(u => u.Type == UserType.Company, ct);
        var individuals = await db.Users.AsNoTracking().CountAsync(u => u.Type == UserType.Individual, ct);

        return new AppUserCountsDto(sellers, buyers, companies, individuals);
    }
}
