namespace Etamid.Domain.Deals;

/// <summary>
/// The only legal moves between deal states. The deal status is deliberately separate from the
/// payment status (<c>PaymentStatus</c>): a payment can succeed while the deal is already expired.
/// </summary>
public static class DealStateMachine
{
    private static readonly Dictionary<DealStatus, DealStatus[]> Transitions = new()
    {
        [DealStatus.PendingAcceptance] = [DealStatus.AwaitingFunding, DealStatus.Rejected, DealStatus.Cancelled, DealStatus.Expired],
        [DealStatus.AwaitingFunding] = [DealStatus.Funded, DealStatus.Cancelled, DealStatus.Expired],
        [DealStatus.Funded] = [DealStatus.InDelivery, DealStatus.RefundPending],
        [DealStatus.InDelivery] = [DealStatus.Inspection, DealStatus.Disputed, DealStatus.RefundPending],
        [DealStatus.Inspection] = [DealStatus.Released, DealStatus.Disputed],
        [DealStatus.Disputed] = [DealStatus.Released, DealStatus.RefundPending],
        [DealStatus.RefundPending] = [DealStatus.Refunded],
        [DealStatus.Released] = [],
        [DealStatus.Rejected] = [],
        [DealStatus.Cancelled] = [],
        [DealStatus.Expired] = [],
        [DealStatus.Refunded] = []
    };

    public static bool CanMove(DealStatus from, DealStatus to)
        => Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static bool IsTerminal(DealStatus status)
        => Transitions.TryGetValue(status, out var allowed) && allowed.Length == 0;

    /// <summary>Statuses while money sits in escrow (or may still be claimed from it).</summary>
    public static bool HoldsMoney(DealStatus status)
        => status is DealStatus.Funded or DealStatus.InDelivery or DealStatus.Inspection
            or DealStatus.Disputed or DealStatus.RefundPending;
}
