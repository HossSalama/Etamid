using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals;
using Etamid.Domain.Deals;
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
        IApplicationDbContext db, Deal d, long viewerId, DateTime now, CancellationToken ct)
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

        return new DealDetailDto(
            d.Id, d.Number, d.Title, d.Description, d.Amount, d.Currency, ApiNames.Of(d.Status),
            RoleOf(d, viewerId), other?.Name ?? string.Empty, other?.Code ?? string.Empty,
            d.DurationValue, ApiNames.Of(d.DurationUnit), d.NeedShipping, d.ShippingCompanyId, d.ShippingAddressId,
            d.FeeAmount, d.SellerNetAmount, d.IsDisputed,
            d.CreatedAt, d.RespondDeadlineAt, d.AcceptedAt, d.FundedAt, d.DeliveredAt,
            d.InspectionEndsAt, d.CompletedAt, d.ExpiresAt,
            d.ContractHash, pendingEdit is null ? null : MapEdit(pendingEdit, d),
            DealActions.For(d, viewerId, now, pendingEdit), timeline);
    }

    public static DealEditDto MapEdit(DealEditRequest e, Deal d) => new(
        e.Id, e.DealId, e.ProposedById == d.BuyerId ? "buyer" : "seller", ApiNames.Of(e.Status),
        e.PrevAmount, e.PrevDurationValue, e.PrevDurationUnit, e.PrevDescription,
        e.NewAmount, e.NewDurationValue, e.NewDurationUnit, e.NewDescription,
        e.CreatedAt, e.RespondedAt);

    public static string RoleOf(Deal d, long viewerId) => viewerId == d.BuyerId ? "buyer" : "seller";
}
