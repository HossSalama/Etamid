using Etamid.Domain.Deals;

namespace Etamid.Application.Deals.Services;

/// <summary>
/// The time-left badge shown on a deal (the iOS example: "3 days left").
/// JSON names are text / color / remainingSeconds, as in the OpenAPI Badge schema.
/// </summary>
public sealed record DealBadgeDto(string Text, string Color, int RemainingSeconds);

/// <summary>
/// Computes the badge from the deal's active deadline. The deadline depends on
/// the status: accepting → RespondDeadlineAt, funding → ExpiresAt, inspection →
/// InspectionEndsAt. Every other status has no running clock, so no badge.
/// </summary>
public static class DealBadge
{
    private static readonly TimeSpan YellowWithin = TimeSpan.FromDays(3);
    private static readonly TimeSpan RedWithin = TimeSpan.FromHours(6);

    public static DealBadgeDto? For(
        DealStatus status, DateTime respondDeadlineAt, DateTime? expiresAt, DateTime? inspectionEndsAt, DateTime now)
    {
        DateTime? deadline = status switch
        {
            DealStatus.PendingAcceptance => respondDeadlineAt,
            DealStatus.AwaitingFunding => expiresAt,
            DealStatus.Inspection => inspectionEndsAt,
            _ => null
        };
        if (deadline is null) return null;

        var remaining = deadline.Value - now;

        // The clock ran out but the status still has an active deadline (e.g. the
        // maintenance worker has not expired it yet): show a spent badge, not a
        // negative countdown.
        if (remaining <= TimeSpan.Zero) return new DealBadgeDto("Expired", "grey", 0);

        // Ceiling, so the count never shows a smaller unit than the time left.
        var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var color = remaining > YellowWithin ? "green" : remaining > RedWithin ? "yellow" : "red";

        // Text: whole days while a day or more is left, whole hours below a day,
        // whole minutes below an hour. Hours/minutes use ceiling so the badge never
        // says "0 minutes left" (or "0 hours left") while seconds still remain.
        string text;
        if (remaining >= TimeSpan.FromDays(1))
            text = $"{(int)Math.Floor(remaining.TotalDays)} days left";
        else if (remaining >= TimeSpan.FromHours(1))
            text = $"{(int)Math.Ceiling(remaining.TotalHours)} hours left";
        else
            text = $"{(int)Math.Ceiling(remaining.TotalMinutes)} minutes left";

        return new DealBadgeDto(text, color, remainingSeconds);
    }
}
