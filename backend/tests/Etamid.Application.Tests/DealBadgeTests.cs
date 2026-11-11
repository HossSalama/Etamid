using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;

namespace Etamid.Application.Tests;

public class DealBadgeTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void More_than_three_days_is_green_with_whole_days()
    {
        var badge = DealBadge.For(DealStatus.PendingAcceptance, Now.AddDays(5), null, null, Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("5 days left");
        badge.Color.Should().Be("green");
        badge.RemainingSeconds.Should().Be(5 * 24 * 3600);
    }

    [Fact]
    public void Exactly_three_days_is_yellow_like_the_iOS_example()
    {
        var badge = DealBadge.For(DealStatus.PendingAcceptance, Now.AddDays(3), null, null, Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("3 days left");
        badge.Color.Should().Be("yellow");
        badge.RemainingSeconds.Should().Be(259200);
    }

    [Fact]
    public void Six_hours_is_red()
    {
        var badge = DealBadge.For(DealStatus.PendingAcceptance, Now.AddHours(6), null, null, Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("6 hours left");
        badge.Color.Should().Be("red");
        badge.RemainingSeconds.Should().Be(6 * 3600);
    }

    [Fact]
    public void Under_an_hour_shows_minutes_rounded_up()
    {
        // 30m30s left: ceiling, so the badge never says "0 minutes left" while
        // seconds still remain.
        var badge = DealBadge.For(DealStatus.PendingAcceptance, Now.AddMinutes(30).AddSeconds(30), null, null, Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("31 minutes left");
        badge.Color.Should().Be("red");
        badge.RemainingSeconds.Should().Be(1830);
    }

    [Fact]
    public void Past_deadline_is_grey_expired_with_zero_seconds()
    {
        var badge = DealBadge.For(DealStatus.PendingAcceptance, Now.AddHours(-1), null, null, Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("Expired");
        badge.Color.Should().Be("grey");
        badge.RemainingSeconds.Should().Be(0);
    }

    [Theory]
    [InlineData(DealStatus.Funded)]
    [InlineData(DealStatus.InDelivery)]
    [InlineData(DealStatus.Disputed)]
    [InlineData(DealStatus.RefundPending)]
    [InlineData(DealStatus.Released)]
    [InlineData(DealStatus.Rejected)]
    [InlineData(DealStatus.Cancelled)]
    [InlineData(DealStatus.Expired)]
    [InlineData(DealStatus.Refunded)]
    public void Statuses_without_an_active_deadline_have_no_badge(DealStatus status)
    {
        DealBadge.For(status, Now.AddDays(2), Now.AddDays(2), Now.AddDays(2), Now)
            .Should().BeNull();
    }

    [Fact]
    public void PendingAcceptance_uses_the_respond_deadline()
    {
        var badge = DealBadge.For(DealStatus.PendingAcceptance, Now.AddDays(2), Now.AddDays(10), Now.AddDays(10), Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("2 days left");
        badge.Color.Should().Be("yellow");
    }

    [Fact]
    public void AwaitingFunding_uses_the_funding_expiry()
    {
        var badge = DealBadge.For(DealStatus.AwaitingFunding, Now.AddHours(1), Now.AddDays(5), null, Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("5 days left");
        badge.Color.Should().Be("green");
    }

    [Fact]
    public void Inspection_uses_the_inspection_end()
    {
        var badge = DealBadge.For(DealStatus.Inspection, Now.AddDays(9), Now.AddDays(9), Now.AddHours(2), Now);

        badge.Should().NotBeNull();
        badge!.Text.Should().Be("2 hours left");
        badge.Color.Should().Be("red");
    }

    [Fact]
    public void Missing_deadline_for_an_active_status_has_no_badge()
    {
        DealBadge.For(DealStatus.AwaitingFunding, Now.AddHours(1), null, null, Now).Should().BeNull();
        DealBadge.For(DealStatus.Inspection, Now.AddDays(1), null, null, Now).Should().BeNull();
    }
}
