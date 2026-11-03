using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Queries;

public sealed record GetMyDealsQuery(string? Role, string? Tab, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<DealSummaryDto>>>;

public sealed class GetMyDealsHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files)
    : IRequestHandler<GetMyDealsQuery, Result<PagedResult<DealSummaryDto>>>
{
    public async Task<Result<PagedResult<DealSummaryDto>>> Handle(GetMyDealsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var role = r.Role?.Trim().ToLowerInvariant();
        if (role is not (null or "" or "buyer" or "seller")) return Error.Validation("INVALID_VALUE", "role");

        var tab = r.Tab?.Trim().ToLowerInvariant();
        if (tab is not (null or "" or DealCategory.New or DealCategory.InProgress or DealCategory.Completed))
            return Error.Validation("INVALID_VALUE", "tab");

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.Deals.AsNoTracking().Where(d => d.BuyerId == userId || d.SellerId == userId);
        if (role == "buyer") query = query.Where(d => d.BuyerId == userId);
        if (role == "seller") query = query.Where(d => d.SellerId == userId);
        if (!string.IsNullOrEmpty(tab))
        {
            var statuses = DealActions.StatusesOf(tab);
            query = query.Where(d => statuses.Contains(d.Status));
        }

        var total = await query.CountAsync(ct);
        var deals = await query
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var otherIds = deals.Select(d => d.BuyerId == userId ? d.SellerId : d.BuyerId).Distinct().ToList();
        var others = await db.Users.AsNoTracking()
            .Where(u => otherIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Code })
            .ToDictionaryAsync(u => u.Id, ct);

        var now = clock.UtcNow;
        var dealIds = deals.Select(d => d.Id).ToList();
        var pendingEdits = await db.DealEditRequests.AsNoTracking()
            .Where(e => dealIds.Contains(e.DealId) && e.Status == EditStatus.Pending)
            .ToDictionaryAsync(e => e.DealId, ct);

        var ratedDealIds = await db.Ratings.AsNoTracking()
            .Where(x => dealIds.Contains(x.DealId))
            .Select(x => x.DealId)
            .ToHashSetAsync(ct);

        var imageFileIds = deals.Where(d => d.ImageFileId is not null).Select(d => d.ImageFileId!.Value).Distinct().ToList();
        var imageFiles = imageFileIds.Count == 0
            ? new Dictionary<long, Domain.Files.StoredFile>()
            : await db.Files.AsNoTracking().Where(f => imageFileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        var companyIds = deals.Where(d => d.ShippingCompanyId is not null).Select(d => d.ShippingCompanyId!.Value).Distinct().ToList();
        var companies = companyIds.Count == 0
            ? new Dictionary<long, Domain.Config.ShippingCompany>()
            : await db.ShippingCompanies.AsNoTracking().Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var addressIds = deals.Where(d => d.ShippingAddressId is not null).Select(d => d.ShippingAddressId!.Value).Distinct().ToList();
        var addresses = addressIds.Count == 0
            ? new Dictionary<long, Domain.Identity.Address>()
            : await db.Addresses.AsNoTracking().Where(a => addressIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        var shipments = await db.Shipments.AsNoTracking()
            .Where(s => dealIds.Contains(s.DealId)).ToDictionaryAsync(s => s.DealId, ct);

        IReadOnlyList<DealSummaryDto> items = deals.Select(d =>
        {
            var otherId = d.BuyerId == userId ? d.SellerId : d.BuyerId;
            others.TryGetValue(otherId, out var other);
            pendingEdits.TryGetValue(d.Id, out var pendingEdit);
            string? imageUrl = null;
            if (d.ImageFileId is { } imageFileId && imageFiles.TryGetValue(imageFileId, out var imageFile))
                imageUrl = files.Resolve(imageFile);
            companies.TryGetValue(d.ShippingCompanyId ?? -1, out var company);
            var addressText = d.ShippingAddressSnapshot;
            if (addressText is null && d.ShippingAddressId is { } addressId
                && addresses.TryGetValue(addressId, out var address))
                addressText = DealMapper.ShippingSnapshotText(address);
            shipments.TryGetValue(d.Id, out var shipment);
            return new DealSummaryDto(
                d.Id, d.Number, d.Title, d.Amount, d.Currency, ApiNames.Of(d.Status),
                DealMapper.RoleOf(d, userId), other?.Name ?? string.Empty, other?.Code ?? string.Empty,
                d.CreatedAt, DealActions.For(d, userId, now, pendingEdit, ratedDealIds.Contains(d.Id)), imageUrl,
                d.NeedShipping, company?.NameAr, company?.NameEn, addressText,
                shipment?.TrackingNumber, shipment is null ? null : ApiNames.Of(shipment.Status), otherId);
        }).ToList();

        return new PagedResult<DealSummaryDto>(items, page, pageSize, total);
    }
}

/// <summary>Manual tracking (v1): the shipment created at delivery, if any. No shipment yet means no tracking.</summary>
public sealed record GetDealTrackingQuery(long DealId) : IRequest<Result<ShipmentTrackingDto>>;

public sealed class GetDealTrackingHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetDealTrackingQuery, Result<ShipmentTrackingDto>>
{
    public async Task<Result<ShipmentTrackingDto>> Handle(GetDealTrackingQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        var (companyAr, companyEn, _, tracking, shipmentStatus) =
            await DealMapper.ShippingInfoAsync(db, deal, ct);

        var shipment = await db.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.DealId == deal.Id, ct);
        var events = shipment is null
            ? []
            : await db.ShipmentEvents.AsNoTracking()
                .Where(e => e.ShipmentId == shipment.Id)
                .OrderBy(e => e.EventAt).ThenBy(e => e.Id)
                .Select(e => new ShipmentEventDto(e.Code, e.Description, e.Location, e.EventAt))
                .ToListAsync(ct);

        return Result.Success(new ShipmentTrackingDto(
            deal.Id, tracking, shipmentStatus, companyAr, companyEn, events));
    }
}

public sealed record GetDealQuery(long DealId) : IRequest<Result<DealDetailDto>>;

public sealed class GetDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files)
    : IRequestHandler<GetDealQuery, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(GetDealQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == r.DealId, ct);

        // A stranger gets the same 404 as a missing deal, so ids cannot be probed.
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        return await DealMapper.DetailAsync(db, deal, userId, clock.UtcNow, ct, files);
    }
}
