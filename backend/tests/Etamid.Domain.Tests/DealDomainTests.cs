using Etamid.Domain.Deals;
using Etamid.Domain.Money;

namespace Etamid.Domain.Tests;

public class DealDomainTests
{
    private const long Buyer = 1, Seller = 2, Stranger = 3;
    private static readonly DateTime T0 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Deal NewDeal(decimal amount = 1000m, int hours = 24)
        => Deal.Create(Buyer, Seller, "Phone", "Used phone", amount, hours, DurationUnit.Hour,
            false, null, null, null, "SAR", T0, TimeSpan.FromHours(48)).Value;

    private static Deal Inspecting()
    {
        var deal = NewDeal();
        deal.Accept(Seller, 50m, T0, TimeSpan.FromHours(48)).IsSuccess.Should().BeTrue();
        deal.MarkFunded(T0).IsSuccess.Should().BeTrue();
        deal.Deliver(Seller, T0).IsSuccess.Should().BeTrue();
        return deal;
    }

    // ---------------------------------------------------------------- creation
    [Fact]
    public void Create_starts_pending_acceptance()
    {
        var deal = NewDeal();
        deal.Status.Should().Be(DealStatus.PendingAcceptance);
        deal.ContractSnapshot.Should().BeNull("terms are only frozen once the seller accepts");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.123)]
    public void Create_rejects_invalid_amounts(double amount)
        => Deal.Create(Buyer, Seller, "t", "d", (decimal)amount, 1, DurationUnit.Day, false, null, null, null, "SAR", T0, TimeSpan.FromHours(1))
            .Error.Code.Should().Be("DEAL_AMOUNT_INVALID");

    [Fact]
    public void Create_rejects_same_buyer_and_seller()
        => Deal.Create(Buyer, Buyer, "t", "d", 10m, 1, DurationUnit.Day, false, null, null, null, "SAR", T0, TimeSpan.FromHours(1))
            .Error.Code.Should().Be("DEAL_SAME_PARTIES");

    // ---------------------------------------------------------------- accept / contract snapshot
    [Fact]
    public void Accept_freezes_contract_and_moves_to_awaiting_funding()
    {
        var deal = NewDeal();
        deal.Accept(Seller, 50m, T0, TimeSpan.FromHours(48)).IsSuccess.Should().BeTrue();

        deal.Status.Should().Be(DealStatus.AwaitingFunding);
        deal.FeeAmount.Should().Be(50m);
        deal.SellerNetAmount.Should().Be(950m);
        deal.ContractHash.Should().NotBeNullOrEmpty();
        deal.IsContractIntact().Should().BeTrue();
    }

    [Fact]
    public void Only_the_seller_can_accept_or_reject()
    {
        NewDeal().Accept(Buyer, 50m, T0, TimeSpan.FromHours(1)).Error.Code.Should().Be("DEAL_ACTION_NOT_ALLOWED");
        NewDeal().Accept(Stranger, 50m, T0, TimeSpan.FromHours(1)).Error.Code.Should().Be("DEAL_ACTION_NOT_ALLOWED");
        NewDeal().Reject(Buyer, 1, null, T0).Error.Code.Should().Be("DEAL_ACTION_NOT_ALLOWED");
    }

    [Fact]
    public void Accept_after_response_deadline_fails()
        => NewDeal().Accept(Seller, 50m, T0.AddHours(49), TimeSpan.FromHours(1)).Error.Code.Should().Be("DEAL_RESPONSE_EXPIRED");

    [Fact]
    public void Fee_must_be_lower_than_amount()
        => NewDeal(40m).Accept(Seller, 50m, T0, TimeSpan.FromHours(1)).Error.Code.Should().Be("DEAL_AMOUNT_TOO_LOW");

    [Fact]
    public void Reject_is_terminal()
    {
        var deal = NewDeal();
        deal.Reject(Seller, 1, "no", T0).IsSuccess.Should().BeTrue();
        deal.Status.Should().Be(DealStatus.Rejected);
        deal.Accept(Seller, 50m, T0, TimeSpan.FromHours(1)).IsFailure.Should().BeTrue();
    }

    // ---------------------------------------------------------------- cancel / expire only before funding
    [Fact]
    public void Cancel_is_allowed_before_funding_only()
    {
        var early = NewDeal();
        early.Cancel(Buyer, T0).IsSuccess.Should().BeTrue();
        early.Status.Should().Be(DealStatus.Cancelled);

        var funded = NewDeal();
        funded.Accept(Seller, 50m, T0, TimeSpan.FromHours(48));
        funded.MarkFunded(T0);
        funded.Cancel(Buyer, T0).Error.Code.Should().Be("DEAL_INVALID_STATE");
    }

    [Fact]
    public void Expire_only_after_deadline_and_never_once_funded()
    {
        var deal = NewDeal();
        deal.Expire(T0.AddHours(1)).IsFailure.Should().BeTrue();
        deal.Expire(T0.AddHours(49)).IsSuccess.Should().BeTrue();
        deal.Status.Should().Be(DealStatus.Expired);

        var funded = NewDeal();
        funded.Accept(Seller, 50m, T0, TimeSpan.FromHours(48));
        funded.MarkFunded(T0);
        funded.Expire(T0.AddDays(30)).IsFailure.Should().BeTrue();
    }

    // ---------------------------------------------------------------- delivery / inspection / release
    [Fact]
    public void Deliver_starts_the_inspection_clock()
    {
        var deal = Inspecting();
        deal.Status.Should().Be(DealStatus.Inspection);
        deal.InspectionEndsAt.Should().Be(T0.AddHours(24));
        deal.History.Select(h => h.ToStatus).Should().ContainInOrder(
            "PendingAcceptance", "AwaitingFunding", "Funded", "InDelivery", "Inspection");
    }

    [Fact]
    public void Deliver_requires_funding_and_the_seller()
    {
        var unfunded = NewDeal();
        unfunded.Accept(Seller, 50m, T0, TimeSpan.FromHours(48));
        unfunded.Deliver(Seller, T0).Error.Code.Should().Be("DEAL_INVALID_STATE");

        var funded = NewDeal();
        funded.Accept(Seller, 50m, T0, TimeSpan.FromHours(48));
        funded.MarkFunded(T0);
        funded.Deliver(Buyer, T0).Error.Code.Should().Be("DEAL_ACTION_NOT_ALLOWED");
    }

    [Fact]
    public void Buyer_confirms_receipt_but_seller_cannot()
    {
        var deal = Inspecting();
        deal.ConfirmReceipt(Seller, T0).Error.Code.Should().Be("DEAL_ACTION_NOT_ALLOWED");
        deal.ConfirmReceipt(Stranger, T0).Error.Code.Should().Be("DEAL_ACTION_NOT_ALLOWED");
        deal.ConfirmReceipt(Buyer, T0.AddHours(1)).IsSuccess.Should().BeTrue();
        deal.Status.Should().Be(DealStatus.Released);
    }

    [Fact]
    public void Auto_release_waits_for_the_inspection_window()
    {
        var deal = Inspecting();
        deal.AutoRelease(T0.AddHours(23)).Error.Code.Should().Be("DEAL_INSPECTION_NOT_ENDED");
        deal.AutoRelease(T0.AddHours(24)).IsSuccess.Should().BeTrue();
        deal.Status.Should().Be(DealStatus.Released);
    }

    // ---------------------------------------------------------------- dispute freezes release
    [Fact]
    public void Dispute_stops_buyer_release_and_auto_release()
    {
        var deal = Inspecting();
        deal.OpenDispute(Buyer, T0.AddHours(2)).IsSuccess.Should().BeTrue();

        deal.Status.Should().Be(DealStatus.Disputed);
        deal.IsDisputed.Should().BeTrue();
        deal.AutoRelease(T0.AddDays(10)).Error.Code.Should().Be("DEAL_DISPUTED");
        deal.ConfirmReceipt(Buyer, T0.AddDays(10)).Error.Code.Should().Be("DEAL_DISPUTED");
        deal.OpenDispute(Buyer, T0.AddHours(3)).Error.Code.Should().Be("DEAL_DISPUTED");
    }

    [Fact]
    public void Only_the_buyer_can_dispute_and_only_during_inspection()
    {
        Inspecting().OpenDispute(Seller, T0).Error.Code.Should().Be("DEAL_ACTION_NOT_ALLOWED");
        Inspecting().OpenDispute(Buyer, T0.AddHours(25)).Error.Code.Should().Be("DEAL_INSPECTION_ENDED");

        var funded = NewDeal();
        funded.Accept(Seller, 50m, T0, TimeSpan.FromHours(48));
        funded.MarkFunded(T0);
        funded.OpenDispute(Buyer, T0).Error.Code.Should().Be("DEAL_INVALID_STATE");
    }

    [Fact]
    public void Released_deal_accepts_no_further_actions()
    {
        var deal = Inspecting();
        deal.ConfirmReceipt(Buyer, T0.AddHours(1));
        deal.ConfirmReceipt(Buyer, T0.AddHours(2)).IsFailure.Should().BeTrue();
        deal.OpenDispute(Buyer, T0.AddHours(2)).IsFailure.Should().BeTrue();
        deal.AutoRelease(T0.AddDays(5)).IsFailure.Should().BeTrue();
    }
}

public class DealStateMachineTests
{
    [Theory]
    [InlineData(DealStatus.PendingAcceptance, DealStatus.AwaitingFunding)]
    [InlineData(DealStatus.AwaitingFunding, DealStatus.Funded)]
    [InlineData(DealStatus.Funded, DealStatus.InDelivery)]
    [InlineData(DealStatus.InDelivery, DealStatus.Inspection)]
    [InlineData(DealStatus.Inspection, DealStatus.Released)]
    [InlineData(DealStatus.Inspection, DealStatus.Disputed)]
    [InlineData(DealStatus.PendingAcceptance, DealStatus.Rejected)]
    [InlineData(DealStatus.RefundPending, DealStatus.Refunded)]
    public void Legal_moves_are_allowed(DealStatus from, DealStatus to)
        => DealStateMachine.CanMove(from, to).Should().BeTrue();

    [Theory]
    [InlineData(DealStatus.PendingAcceptance, DealStatus.Funded)]
    [InlineData(DealStatus.AwaitingFunding, DealStatus.Released)]
    [InlineData(DealStatus.Funded, DealStatus.Released)]
    [InlineData(DealStatus.Funded, DealStatus.Cancelled)]
    [InlineData(DealStatus.Funded, DealStatus.Expired)]
    [InlineData(DealStatus.Inspection, DealStatus.Cancelled)]
    [InlineData(DealStatus.Released, DealStatus.Disputed)]
    [InlineData(DealStatus.Rejected, DealStatus.AwaitingFunding)]
    [InlineData(DealStatus.Refunded, DealStatus.Funded)]
    public void Illegal_moves_are_refused(DealStatus from, DealStatus to)
        => DealStateMachine.CanMove(from, to).Should().BeFalse();

    [Fact]
    public void Terminal_states_have_no_exit()
    {
        foreach (var s in new[] { DealStatus.Released, DealStatus.Rejected, DealStatus.Cancelled, DealStatus.Expired, DealStatus.Refunded })
            DealStateMachine.IsTerminal(s).Should().BeTrue();
    }
}

public class EscrowPostingsTests
{
    private const long Clearing = 10, Escrow = 20, Wallet = 30, Revenue = 40;

    private static decimal Sum(IEnumerable<PostingLine> l, LedgerDirection d) => l.Where(x => x.Direction == d).Sum(x => x.Amount);

    [Fact]
    public void Funding_is_dr_gateway_clearing_cr_escrow()
    {
        var lines = EscrowPostings.Funding(1000m, Clearing, Escrow).Value;
        lines.Should().ContainSingle(l => l.AccountId == Clearing && l.Direction == LedgerDirection.Debit && l.Amount == 1000m);
        lines.Should().ContainSingle(l => l.AccountId == Escrow && l.Direction == LedgerDirection.Credit && l.Amount == 1000m);
    }

    [Fact]
    public void Release_splits_950_to_seller_and_50_to_platform_and_balances()
    {
        var lines = EscrowPostings.Release(1000m, 50m, Escrow, Wallet, Revenue).Value;

        lines.Should().ContainSingle(l => l.AccountId == Escrow && l.Direction == LedgerDirection.Debit && l.Amount == 1000m);
        lines.Should().ContainSingle(l => l.AccountId == Wallet && l.Direction == LedgerDirection.Credit && l.Amount == 950m);
        lines.Should().ContainSingle(l => l.AccountId == Revenue && l.Direction == LedgerDirection.Credit && l.Amount == 50m);
        Sum(lines, LedgerDirection.Debit).Should().Be(Sum(lines, LedgerDirection.Credit));
    }

    [Fact]
    public void Release_with_zero_fee_writes_no_revenue_line()
    {
        var lines = EscrowPostings.Release(500m, 0m, Escrow, Wallet, Revenue).Value;
        lines.Should().HaveCount(2);
        lines.Should().NotContain(l => l.AccountId == Revenue);
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(1000, 1500)]
    [InlineData(1000, -1)]
    public void Release_refuses_a_fee_that_is_not_below_the_amount(double amount, double fee)
        => EscrowPostings.Release((decimal)amount, (decimal)fee, Escrow, Wallet, Revenue).IsFailure.Should().BeTrue();

    [Fact]
    public void Validate_rejects_unbalanced_single_line_and_fractional_postings()
    {
        LedgerPosting.Validate([new(1, LedgerDirection.Debit, 100m), new(2, LedgerDirection.Credit, 99m)]).IsFailure.Should().BeTrue();
        LedgerPosting.Validate([new(1, LedgerDirection.Debit, 100m)]).IsFailure.Should().BeTrue();
        LedgerPosting.Validate([new(1, LedgerDirection.Debit, 10.005m), new(2, LedgerDirection.Credit, 10.005m)]).IsFailure.Should().BeTrue();
        LedgerPosting.Validate([new(1, LedgerDirection.Debit, 100m), new(2, LedgerDirection.Credit, 100m)]).IsSuccess.Should().BeTrue();
    }
}
