using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Identity;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Disputes;

/// <summary>
/// Executes a dispute decision (or an accepted settlement) against the escrow hold.
/// The money moves in the same unit of work as the deal/dispute transitions, and a
/// replayed execution is impossible: the dispute closes in the same save, and the
/// ledger carries an idempotency key per deal.
/// </summary>
public sealed class DisputeExecutionService(IApplicationDbContext db, EscrowLedgerService ledger, IClock clock)
{
    public async Task<Result<DisputeExecutionDto>> ExecuteAsync(
        Deal deal, Dispute dispute, DisputeResolution resolution, int? sellerPercent, CreatedByType createdBy, CancellationToken ct)
    {
        if (!DisputeMapper.IsOpen(dispute.Status)) return DisputeErrors.AlreadyResolved;
        if (deal.Status != DealStatus.Disputed || !deal.IsDisputed) return DisputeErrors.InvalidState;
        if (!deal.IsContractIntact()) return DealErrors.ContractTampered;

        var hold = await db.EscrowHolds.FirstOrDefaultAsync(h => h.DealId == deal.Id, ct);
        if (hold is null || hold.Status != EscrowStatus.FrozenDispute) return DealErrors.EscrowNotHeld;

        var now = clock.UtcNow;
        var fee = deal.FeeAmount ?? 0m;

        // The seller's share of the deal amount (the whole amount, nothing, or the agreed percent).
        decimal sellerShare;
        switch (resolution)
        {
            case DisputeResolution.ReleaseSeller:
                sellerShare = deal.Amount;
                break;
            case DisputeResolution.RefundBuyer:
                sellerShare = 0m;
                break;
            case DisputeResolution.Partial:
                if (sellerPercent is not { } pct || pct is < 1 or > 99)
                    return Error.Validation("INVALID_VALUE", "sellerPercent");
                sellerShare = decimal.Round(deal.Amount * pct / 100m, 2);
                break;
            default:
                return Error.Validation("INVALID_VALUE", "resolution");
        }

        // Wallets are created first: those saves must not carry half-applied deal changes.
        Wallet? sellerWallet = null;
        Wallet? buyerWallet = null;
        if (sellerShare > 0) sellerWallet = await ledger.GetOrCreateWalletAsync(deal.SellerId, ct);
        if (sellerShare < deal.Amount) buyerWallet = await ledger.GetOrCreateWalletAsync(deal.BuyerId, ct);
        await ledger.EnsurePlatformAccountsAsync(ct);

        var reason = resolution == DisputeResolution.RefundBuyer ? ReleaseReason.DisputeBuyer : ReleaseReason.DisputeSeller;
        var staged = await ledger.StageDisputeExecutionAsync(
            deal, hold, resolution, sellerShare, sellerWallet, buyerWallet, reason, createdBy, ct);
        if (staged.IsFailure) return staged.Error;

        var moved = resolution == DisputeResolution.RefundBuyer
            ? deal.ResolveDisputeRefund(now)
            : deal.ResolveDisputeRelease(now);
        if (moved.IsFailure) return moved.Error;

        // A release to the seller is a successful deal for the seller — the same rule
        // as the normal release path (buyer confirmation and automatic release).
        if (sellerShare > 0)
        {
            var store = await db.StoreProfiles.FirstOrDefaultAsync(s => s.UserId == deal.SellerId, ct);
            if (store is null)
            {
                var seller = await db.Users.FirstOrDefaultAsync(u => u.Id == deal.SellerId, ct);
                store = new StoreProfile { UserId = deal.SellerId, StoreName = seller?.Name ?? string.Empty };
                db.StoreProfiles.Add(store);
            }
            store.SuccessfulDeals += 1;
        }

        dispute.Status = DisputeStatus.Closed;
        dispute.Resolution = resolution;
        dispute.ResolvedAt = now;
        // ResolvedByAdminId stays null: it points at Cycle 9 AdminUsers, not app users
        // (same as the wallet's ApprovedByAdminId and the company waiver's WaivedByAdminId).

        return new DisputeExecutionDto(
            dispute.Id, dispute.Number, ApiNames.Of(resolution),
            staged.Value.SellerAmount, staged.Value.BuyerAmount, fee, ApiNames.Of(deal.Status));
    }
}
