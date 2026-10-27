using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Deals;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Services;

/// <summary>
/// Writes escrow money movements as balanced double-entry transactions. It never changes a balance
/// by itself: a wallet's AvailableBalance is only a projection set from the ledger sum, in the same
/// unit of work as the entries (the caller saves).
/// </summary>
public sealed class EscrowLedgerService(IApplicationDbContext db, IClock clock)
{
    /// <summary>Makes sure the three platform accounts exist (saves only when one is created).</summary>
    public async Task EnsurePlatformAccountsAsync(CancellationToken ct)
    {
        await GetPlatformAccountAsync(LedgerAccountType.GatewayClearing, ct);
        await GetPlatformAccountAsync(LedgerAccountType.PlatformEscrow, ct);
        await GetPlatformAccountAsync(LedgerAccountType.PlatformRevenue, ct);
    }

    public async Task<long> GetPlatformAccountAsync(LedgerAccountType type, CancellationToken ct)
    {
        var existing = await FindPlatformAccountAsync(type, ct);
        if (existing is { } id) return id;

        var account = new LedgerAccount { Type = type, OwnerUserId = null, Currency = "SAR" };
        db.LedgerAccounts.Add(account);
        try
        {
            await db.SaveChangesAsync(ct);
            return account.Id;
        }
        catch (DbUpdateException)
        {
            // Another request created the same platform account first (unique index): use theirs.
            db.LedgerAccounts.Remove(account);
            return await FindPlatformAccountAsync(type, ct) ?? throw new InvalidOperationException($"Ledger account {type} could not be created.");
        }
    }

    private async Task<long?> FindPlatformAccountAsync(LedgerAccountType type, CancellationToken ct)
        => await db.LedgerAccounts.AsNoTracking()
            .Where(a => a.OwnerUserId == null && a.Type == type)
            .OrderBy(a => a.Id)
            .Select(a => (long?)a.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>The user's wallet and its ledger account, created on first use.</summary>
    public async Task<Wallet> GetOrCreateWalletAsync(long userId, CancellationToken ct)
    {
        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet is not null) return wallet;

        var account = new LedgerAccount { Type = LedgerAccountType.UserWallet, OwnerUserId = userId, Currency = "SAR" };
        db.LedgerAccounts.Add(account);
        await db.SaveChangesAsync(ct);

        wallet = new Wallet { UserId = userId, LedgerAccountId = account.Id, Activated = false, AvailableBalance = 0m };
        db.Wallets.Add(wallet);
        try
        {
            await db.SaveChangesAsync(ct);
            return wallet;
        }
        catch (DbUpdateException)
        {
            db.Wallets.Remove(wallet);
            return await db.Wallets.FirstAsync(w => w.UserId == userId, ct);
        }
    }

    /// <summary>
    /// Buyer's payment confirmed: Dr GatewayClearing / Cr PlatformEscrow, plus the escrow hold.
    /// Idempotent per payment: a second call stages nothing.
    /// </summary>
    public async Task<Result> StageFundingAsync(Deal deal, Payment payment, CancellationToken ct)
    {
        var key = $"escrow-fund:{payment.Id}";
        if (await db.LedgerTransactions.AnyAsync(t => t.IdempotencyKey == key, ct)) return Result.Success();

        var gateway = await GetPlatformAccountAsync(LedgerAccountType.GatewayClearing, ct);
        var escrow = await GetPlatformAccountAsync(LedgerAccountType.PlatformEscrow, ct);

        var lines = EscrowPostings.Funding(deal.Amount, gateway, escrow);
        if (lines.IsFailure) return lines.Error;

        db.LedgerTransactions.Add(BuildTransaction(
            LedgerTransactionType.EscrowFunding, deal.Id, payment.Id, key, $"Escrow funding for deal {deal.Id}", lines.Value));

        db.EscrowHolds.Add(new EscrowHold
        {
            DealId = deal.Id,
            BuyerId = deal.BuyerId,
            SellerId = deal.SellerId,
            Amount = deal.Amount,
            FeeAmount = deal.FeeAmount,
            Status = EscrowStatus.Held,
            PaymentId = payment.Id,
            HeldAt = clock.UtcNow
        });
        return Result.Success();
    }

    /// <summary>
    /// Releases the hold to the seller: Dr PlatformEscrow (full amount) / Cr seller wallet (net) / Cr PlatformRevenue (fee).
    /// The seller's wallet must already exist (<see cref="GetOrCreateWalletAsync"/>).
    /// </summary>
    public async Task<Result> StageReleaseAsync(Deal deal, EscrowHold hold, Wallet sellerWallet, ReleaseReason reason, CancellationToken ct)
    {
        if (hold.Status != EscrowStatus.Held) return DealErrors.EscrowNotHeld;

        var key = $"escrow-release:{deal.Id}";
        if (await db.LedgerTransactions.AnyAsync(t => t.IdempotencyKey == key, ct)) return DealErrors.EscrowNotHeld;

        var escrow = await GetPlatformAccountAsync(LedgerAccountType.PlatformEscrow, ct);
        var revenue = await GetPlatformAccountAsync(LedgerAccountType.PlatformRevenue, ct);
        var fee = deal.FeeAmount ?? 0m;

        var lines = EscrowPostings.Release(deal.Amount, fee, escrow, sellerWallet.LedgerAccountId, revenue);
        if (lines.IsFailure) return lines.Error;

        var transaction = BuildTransaction(
            LedgerTransactionType.EscrowRelease, deal.Id, hold.PaymentId, key, $"Escrow release for deal {deal.Id}", lines.Value);

        // Wallet balance is a projection of the ledger: committed entries + the credit staged here.
        var committed = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId == sellerWallet.LedgerAccountId)
            .SumAsync(e => e.Direction == LedgerDirection.Credit ? e.Amount : -e.Amount, ct);
        var credit = lines.Value.Where(l => l.AccountId == sellerWallet.LedgerAccountId).Sum(l => l.Amount);
        var balanceAfter = committed + credit;

        transaction.Entries.First(e => e.AccountId == sellerWallet.LedgerAccountId).BalanceAfter = balanceAfter;
        sellerWallet.AvailableBalance = balanceAfter;
        db.LedgerTransactions.Add(transaction);

        hold.Status = EscrowStatus.Released;
        hold.ReleasedAt = clock.UtcNow;
        hold.ReleaseReason = reason;
        return Result.Success();
    }

    private LedgerTransaction BuildTransaction(
        LedgerTransactionType type, long dealId, long paymentId, string key, string description, IReadOnlyList<PostingLine> lines)
    {
        var transaction = new LedgerTransaction
        {
            PublicId = Guid.NewGuid(),
            Type = type,
            DealId = dealId,
            PaymentId = paymentId,
            Description = description,
            IdempotencyKey = key,
            CreatedByType = CreatedByType.System
        };

        foreach (var line in lines)
            transaction.Entries.Add(new LedgerEntry { AccountId = line.AccountId, Direction = line.Direction, Amount = line.Amount });

        return transaction;
    }
}
