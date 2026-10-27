using Etamid.SharedKernel;

namespace Etamid.Domain.Money;

/// <summary>One side of a double-entry posting, before it becomes a <see cref="LedgerEntry"/>.</summary>
public sealed record PostingLine(long AccountId, LedgerDirection Direction, decimal Amount);

public static class LedgerPosting
{
    /// <summary>
    /// A posting is valid only when it has at least two lines, every amount is positive with at most
    /// two decimals, and total debits equal total credits.
    /// </summary>
    public static Result Validate(IReadOnlyCollection<PostingLine> lines)
    {
        if (lines.Count < 2) return Etamid.Domain.Deals.DealErrors.LedgerUnbalanced;
        if (lines.Any(l => l.Amount <= 0 || decimal.Round(l.Amount, 2) != l.Amount))
            return Etamid.Domain.Deals.DealErrors.LedgerUnbalanced;

        var debits = lines.Where(l => l.Direction == LedgerDirection.Debit).Sum(l => l.Amount);
        var credits = lines.Where(l => l.Direction == LedgerDirection.Credit).Sum(l => l.Amount);
        return debits == credits ? Result.Success() : Etamid.Domain.Deals.DealErrors.LedgerUnbalanced;
    }
}

/// <summary>
/// The only postings the escrow cycle may write. Escrow is a ledger, never an editable balance.
/// </summary>
public static class EscrowPostings
{
    /// <summary>
    /// Buyer's money confirmed by the gateway:
    /// Dr GatewayClearing (asset due from the gateway) / Cr PlatformEscrow (liability: funds held).
    /// </summary>
    public static Result<IReadOnlyList<PostingLine>> Funding(decimal amount, long gatewayClearingAccountId, long platformEscrowAccountId)
    {
        IReadOnlyList<PostingLine> lines =
        [
            new(gatewayClearingAccountId, LedgerDirection.Debit, amount),
            new(platformEscrowAccountId, LedgerDirection.Credit, amount)
        ];
        var check = LedgerPosting.Validate(lines);
        return check.IsFailure ? Result.Failure<IReadOnlyList<PostingLine>>(check.Error) : Result.Success(lines);
    }

    /// <summary>
    /// Release to the seller: Dr PlatformEscrow (close the hold, full amount) /
    /// Cr seller wallet (amount - fee) / Cr PlatformRevenue (fee). A zero fee writes no revenue line.
    /// </summary>
    public static Result<IReadOnlyList<PostingLine>> Release(
        decimal amount, decimal fee, long platformEscrowAccountId, long sellerWalletAccountId, long platformRevenueAccountId)
    {
        if (fee < 0 || fee >= amount) return Etamid.Domain.Deals.DealErrors.AmountTooLow;

        var lines = new List<PostingLine>
        {
            new(platformEscrowAccountId, LedgerDirection.Debit, amount),
            new(sellerWalletAccountId, LedgerDirection.Credit, amount - fee)
        };
        if (fee > 0) lines.Add(new PostingLine(platformRevenueAccountId, LedgerDirection.Credit, fee));

        var check = LedgerPosting.Validate(lines);
        return check.IsFailure
            ? Result.Failure<IReadOnlyList<PostingLine>>(check.Error)
            : Result.Success<IReadOnlyList<PostingLine>>(lines);
    }
}
