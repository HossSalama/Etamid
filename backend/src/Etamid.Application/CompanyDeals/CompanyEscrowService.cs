using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Deals;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.CompanyDeals;

/// <summary>
/// Escrow for company installments, mirroring the individual-deal ledger but
/// without EscrowHold rows: EscrowHold.DealId is a non-nullable FK to
/// individual deals, so it cannot represent an installment. The installment
/// itself (status + succeeded payment) plus the ledger transactions — which
/// carry CompanyDealId and InstallmentId — are the hold record and the audit
/// trail. Stages everything; the caller saves (except wallet/platform
/// account creation, which saves like the deal path does).
/// </summary>
public sealed class CompanyEscrowService(
    IApplicationDbContext db, IClock clock, EscrowLedgerService ledger)
{
    /// <summary>
    /// Payer's payment confirmed: Dr GatewayClearing / Cr PlatformEscrow, for
    /// the funded amount (installment + any penalty included in the session).
    /// Idempotent per payment: a second call stages nothing.
    /// </summary>
    public async Task<Result> StageInstallmentFundingAsync(
        CompanyDeal deal, PaymentInstallment installment, Payment payment, CancellationToken ct)
    {
        var key = $"company-escrow-fund:{payment.Id}";
        if (await db.LedgerTransactions.AnyAsync(t => t.IdempotencyKey == key, ct)) return Result.Success();

        var gateway = await ledger.GetPlatformAccountAsync(LedgerAccountType.GatewayClearing, ct);
        var escrow = await ledger.GetPlatformAccountAsync(LedgerAccountType.PlatformEscrow, ct);

        var lines = EscrowPostings.Funding(payment.Amount, gateway, escrow);
        if (lines.IsFailure) return lines.Error;

        var transaction = new LedgerTransaction
        {
            PublicId = Guid.NewGuid(),
            Type = LedgerTransactionType.EscrowFunding,
            CompanyDealId = deal.Id,
            InstallmentId = installment.Id,
            PaymentId = payment.Id,
            Description = $"Escrow funding for company deal {deal.Id} installment {installment.Id}",
            IdempotencyKey = key,
            CreatedByType = CreatedByType.System
        };
        foreach (var line in lines.Value)
            transaction.Entries.Add(new LedgerEntry { AccountId = line.AccountId, Direction = line.Direction, Amount = line.Amount });
        db.LedgerTransactions.Add(transaction);
        return Result.Success();
    }

    /// <summary>
    /// Releases a funded installment to the creator. The platform fee is the
    /// fixed platform_fee_amount, charged ONCE per company deal (on the first
    /// installment release; later releases take no fee): it is one deal, so
    /// it carries one platform fee, like an individual deal does. A penalty
    /// that was included in the funding session is extra money for the
    /// creator: the release credits amount - fee + penalty, because the
    /// funded amount already contains the penalty. A penalty applied but not
    /// funded stays Applied and is not settled here.
    /// </summary>
    public async Task<Result> StageInstallmentReleaseAsync(
        CompanyDeal deal, PaymentInstallment installment, CancellationToken ct)
    {
        var payment = await db.Payments
            .Where(p => p.InstallmentId == installment.Id && p.Status == PaymentStatus.Succeeded)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
        if (payment is null) return CompanyDealErrors.EscrowNotFunded;

        var key = $"company-escrow-release:{installment.Id}";
        if (await db.LedgerTransactions.AnyAsync(t => t.IdempotencyKey == key, ct))
            return CompanyDealErrors.InvalidState;

        var settings = await DealSettings.LoadAsync(db, ct);
        var alreadyCharged = await db.LedgerTransactions.AnyAsync(
            t => t.CompanyDealId == deal.Id && t.Type == LedgerTransactionType.EscrowRelease, ct);
        var fee = alreadyCharged ? 0m : settings.PlatformFee;

        var wallet = await ledger.GetOrCreateWalletAsync(deal.CreatorUserId, ct);
        await ledger.EnsurePlatformAccountsAsync(ct);
        var escrow = await ledger.GetPlatformAccountAsync(LedgerAccountType.PlatformEscrow, ct);
        var revenue = await ledger.GetPlatformAccountAsync(LedgerAccountType.PlatformRevenue, ct);

        var lines = EscrowPostings.Release(payment.Amount, fee, escrow, wallet.LedgerAccountId, revenue);
        if (lines.IsFailure) return lines.Error;

        var transaction = new LedgerTransaction
        {
            PublicId = Guid.NewGuid(),
            Type = LedgerTransactionType.EscrowRelease,
            CompanyDealId = deal.Id,
            InstallmentId = installment.Id,
            PaymentId = payment.Id,
            Description = $"Escrow release for company deal {deal.Id} installment {installment.Id}",
            IdempotencyKey = key,
            CreatedByType = CreatedByType.System
        };
        foreach (var line in lines.Value)
            transaction.Entries.Add(new LedgerEntry { AccountId = line.AccountId, Direction = line.Direction, Amount = line.Amount });

        // Wallet balance is a projection of the ledger: committed entries + the credit staged here.
        var committed = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId == wallet.LedgerAccountId)
            .SumAsync(e => e.Direction == LedgerDirection.Credit ? e.Amount : -e.Amount, ct);
        var credit = lines.Value.Where(l => l.AccountId == wallet.LedgerAccountId).Sum(l => l.Amount);
        var balanceAfter = committed + credit;

        transaction.Entries.First(e => e.AccountId == wallet.LedgerAccountId).BalanceAfter = balanceAfter;
        wallet.AvailableBalance = balanceAfter;
        db.LedgerTransactions.Add(transaction);

        var now = clock.UtcNow;
        installment.Status = InstallmentStatus.Paid;
        installment.PaidAt = now;

        // The penalty is settled only when the payer actually funded it (the
        // session amount covered amount + penalty). The release transaction
        // itself carries the installment id, which is the settlement link.
        var penalty = await db.PenaltyCharges
            .FirstOrDefaultAsync(p => p.InstallmentId == installment.Id && p.Status == PenaltyStatus.Applied, ct);
        if (penalty is not null && payment.Amount >= installment.Amount + penalty.PenaltyAmount)
            penalty.Status = PenaltyStatus.Paid;

        // All installments paid completes the deal.
        var unpaid = await db.PaymentInstallments
            .AnyAsync(i => i.CompanyDealId == deal.Id && i.Id != installment.Id && i.Status != InstallmentStatus.Paid, ct);
        if (!unpaid && deal.Status == CompanyDealStatus.Active)
        {
            deal.Status = CompanyDealStatus.Completed;
            deal.CompletedAt = now;
            db.CompanyDealStatusHistory.Add(CompanyDealMapper.History(
                deal, CompanyDealStatus.Active, CompanyDealStatus.Completed, deal.CreatorUserId, ActorType.System, null));
        }
        return Result.Success();
    }
}
