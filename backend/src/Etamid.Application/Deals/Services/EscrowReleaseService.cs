using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Deals;
using Etamid.Domain.Identity;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Services;

/// <summary>
/// The single path that moves money out of escrow to the seller. Used by the buyer's confirmation and by the
/// automatic release after the inspection window, so both go through the same checks and the same ledger entry.
/// Stages everything; the caller saves.
/// </summary>
public sealed class EscrowReleaseService(IApplicationDbContext db, EscrowLedgerService ledger)
{
    public async Task<Result> StageReleaseAsync(
        Deal deal, Func<Deal, Result> transition, ReleaseReason reason, CancellationToken ct)
    {
        // A dispute stops every release, whoever asks.
        if (deal.IsDisputed || deal.Status == DealStatus.Disputed) return DealErrors.Disputed;
        if (deal.Status != DealStatus.Inspection) return DealErrors.InvalidState;
        if (!deal.IsContractIntact()) return DealErrors.ContractTampered;

        var hold = await db.EscrowHolds.FirstOrDefaultAsync(h => h.DealId == deal.Id, ct);
        if (hold is null || hold.Status != EscrowStatus.Held) return DealErrors.EscrowNotHeld;

        // Accounts and wallet are created first: those saves must not carry half-applied deal changes.
        var wallet = await ledger.GetOrCreateWalletAsync(deal.SellerId, ct);
        await ledger.EnsurePlatformAccountsAsync(ct);

        var moved = transition(deal);
        if (moved.IsFailure) return moved.Error;

        // A released deal is a successful deal for the seller. The lookup preview
        // and the store page read StoreProfile.SuccessfulDeals, and nothing else
        // updates it, so a release that skips this step leaves it at zero forever.
        // This runs in the single release path (buyer confirmation and automatic
        // release), and the caller saves, so a failed ledger stage or a replayed
        // release (which returns before staging) never double-counts.
        var store = await db.StoreProfiles.FirstOrDefaultAsync(s => s.UserId == deal.SellerId, ct);
        if (store is null)
        {
            var seller = await db.Users.FirstOrDefaultAsync(u => u.Id == deal.SellerId, ct);
            store = new StoreProfile { UserId = deal.SellerId, StoreName = seller?.Name ?? string.Empty };
            db.StoreProfiles.Add(store);
        }
        store.SuccessfulDeals += 1;

        return await ledger.StageReleaseAsync(deal, hold, wallet, reason, ct);
    }
}
