using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals;
using Etamid.Domain.Deals;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.ValueObjects;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Services;

internal static class DealPersistence
{
    /// <summary>Saves; a lost optimistic-concurrency race (double click, parallel request) becomes a 409 instead of a crash.</summary>
    public static async Task<Result> SaveGuardedAsync(this IApplicationDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return DealErrors.InvalidState;
        }
    }
}

internal static class DealMapper
{
    public static async Task<DealDetailDto> DetailAsync(
        IApplicationDbContext db, Deal d, long viewerId, DateTime now, CancellationToken ct, IFileUrlResolver files)
    {
        var otherId = viewerId == d.BuyerId ? d.SellerId : d.BuyerId;
        var other = await db.Users.AsNoTracking().Where(u => u.Id == otherId)
            .Select(u => new { u.Name, u.Code }).FirstOrDefaultAsync(ct);

        var history = await db.DealStatusHistory.AsNoTracking()
            .Where(h => h.DealId == d.Id)
            .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
            .ToListAsync(ct);

        var timeline = history
            .Select(h => new DealTimelineItemDto(h.FromStatus is null ? null : ApiNames.Snake(h.FromStatus), ApiNames.Snake(h.ToStatus), ApiNames.Of(h.ActorType), h.CreatedAt, h.Note))
            .ToList();

        // A pending edit only matters while the deal can still be edited.
        var pendingEdit = d.Status == DealStatus.PendingAcceptance
            ? await db.DealEditRequests.AsNoTracking()
                .Where(e => e.DealId == d.Id && e.Status == EditStatus.Pending)
                .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
                .FirstOrDefaultAsync(ct)
            : null;

        string? imageUrl = null;
        if (d.ImageFileId is { } imageFileId)
            imageUrl = files.Resolve(await db.Files.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == imageFileId, ct));

        var alreadyRated = d.Status == DealStatus.Released
            && await db.Ratings.AsNoTracking().AnyAsync(x => x.DealId == d.Id, ct);

        var (companyAr, companyEn, addressText, tracking, shipmentStatus) =
            await ShippingInfoAsync(db, d, ct);

        return new DealDetailDto(
            d.Id, d.Number, d.Title, d.Description, d.Amount, d.Currency, ApiNames.Of(d.Status),
            RoleOf(d, viewerId), other?.Name ?? string.Empty, other?.Code ?? string.Empty,
            d.DurationValue, ApiNames.Of(d.DurationUnit), d.NeedShipping, d.ShippingCompanyId, d.ShippingAddressId,
            d.FeeAmount, d.SellerNetAmount, d.IsDisputed,
            d.CreatedAt, d.RespondDeadlineAt, d.AcceptedAt, d.FundedAt, d.DeliveredAt,
            d.InspectionEndsAt, d.CompletedAt, d.ExpiresAt,
            d.ContractHash, pendingEdit is null ? null : MapEdit(pendingEdit, d),
            DealActions.For(d, viewerId, now, pendingEdit, alreadyRated), timeline, imageUrl,
            companyAr, companyEn, addressText, tracking, shipmentStatus,
            DealBadge.For(d.Status, d.RespondDeadlineAt, d.ExpiresAt, d.InspectionEndsAt, now));
    }

    /// <summary>
    /// The frozen snapshot is the source of truth; for deals created before snapshots
    /// existed, fall back to the address as it is now (soft-deleted addresses are
    /// already filtered out of queries, so a deleted address yields no text).
    /// </summary>
    public static async Task<(string? CompanyAr, string? CompanyEn, string? AddressText, string? Tracking, string? ShipmentStatus)>
        ShippingInfoAsync(IApplicationDbContext db, Deal d, CancellationToken ct)
    {
        string? companyAr = null, companyEn = null;
        if (d.ShippingCompanyId is { } companyId)
        {
            var company = await db.ShippingCompanies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);
            companyAr = company?.NameAr;
            companyEn = company?.NameEn;
        }

        string? addressText = d.ShippingAddressSnapshot;
        if (addressText is null && d.ShippingAddressId is { } addressId)
        {
            var address = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == addressId, ct);
            if (address is not null) addressText = ShippingSnapshotText(address);
        }

        var shipment = await db.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.DealId == d.Id, ct);
        return (companyAr, companyEn, addressText, shipment?.TrackingNumber,
            shipment is null ? null : ApiNames.Of(shipment.Status));
    }

    /// <summary>Recipient + phone + full address, capped at the 400-char snapshot column.</summary>
    public static string ShippingSnapshotText(Address a)
    {
        var phone = PhoneNumber.TryParse(a.RecipientPhone, out var p) ? p!.Local : a.RecipientPhone;
        var text = $"{a.RecipientName} — {phone} — {a.FullText}";
        return text.Length <= 400 ? text : text[..400];
    }

    public static DealEditDto MapEdit(DealEditRequest e, Deal d) => new(
        e.Id, e.DealId, e.ProposedById == d.BuyerId ? "buyer" : "seller", ApiNames.Of(e.Status),
        e.PrevAmount, e.PrevDurationValue, e.PrevDurationUnit, e.PrevDescription,
        e.NewAmount, e.NewDurationValue, e.NewDurationUnit, e.NewDescription,
        e.CreatedAt, e.RespondedAt);

    public static string RoleOf(Deal d, long viewerId) => viewerId == d.BuyerId ? "buyer" : "seller";
}
