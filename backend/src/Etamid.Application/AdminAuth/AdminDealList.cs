using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Admin;
using Etamid.Domain.Deals;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.AdminAuth;

/// <summary>A deal as seen by an admin: both sides' names, no caller-relative role.</summary>
public sealed record AdminDealSummaryDto(
    long Id, long Number, string Title, decimal Amount, string Currency, string Status,
    long BuyerId, string BuyerName, long SellerId, string SellerName, DateTime CreatedAt);

public sealed record ListAdminDealsQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<AdminDealSummaryDto>>>;

public sealed class ListAdminDealsHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListAdminDealsQuery, Result<PagedResult<AdminDealSummaryDto>>>
{
    public async Task<Result<PagedResult<AdminDealSummaryDto>>> Handle(ListAdminDealsQuery query, CancellationToken ct)
    {
        var guard = await access.RequireAsync(AdminPermissionCodes.DealsView, false, ct);
        if (guard.IsFailure) return guard.Error;

        DealStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var normalized = query.Status.Trim().Replace("_", "");
            if (!Enum.TryParse<DealStatus>(normalized, ignoreCase: true, out var parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var source = db.Deals.AsNoTracking();
        if (status is { } s) source = source.Where(d => d.Status == s);

        var total = await source.CountAsync(ct);
        var deals = await source
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var userIds = deals.SelectMany(d => new[] { d.BuyerId, d.SellerId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name })
            .ToDictionaryAsync(u => u.Id, ct);

        var dtos = deals.Select(d => new AdminDealSummaryDto(
            d.Id, d.Number, d.Title, d.Amount, d.Currency, ApiNames.Of(d.Status),
            d.BuyerId, users.TryGetValue(d.BuyerId, out var buyer) ? buyer.Name : string.Empty,
            d.SellerId, users.TryGetValue(d.SellerId, out var seller) ? seller.Name : string.Empty,
            d.CreatedAt)).ToList();
        return new PagedResult<AdminDealSummaryDto>(dtos, page, pageSize, total);
    }
}
