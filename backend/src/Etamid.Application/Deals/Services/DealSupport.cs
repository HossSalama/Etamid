using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Deals;
using Etamid.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Services;

/// <summary>Business settings of the deal cycle, read from AppSettings with safe defaults.</summary>
public sealed record DealSettings(decimal PlatformFee, TimeSpan RespondWindow, TimeSpan FundingWindow, string Currency)
{
    public static async Task<DealSettings> LoadAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var settings = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        decimal Dec(string key, decimal fallback)
            => settings.TryGetValue(key, out var v) && decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : fallback;

        string Text(string key, string fallback) => settings.TryGetValue(key, out var v) ? v : fallback;

        return new DealSettings(
            Dec("platform_fee_amount", 50m),
            TimeSpan.FromHours((double)Dec("deal_respond_hours", 48m)),
            TimeSpan.FromHours((double)Dec("deal_funding_hours", 48m)),
            Text("currency", "SAR"));
    }
}

/// <summary>Which buttons the given user may press on a deal right now (the API contract's availableActions).</summary>
public static class DealActions
{
    public static IReadOnlyList<string> For(Deal deal, long userId, DateTime now)
        => For(deal, userId, now, pendingEdit: null);

    /// <summary>
    /// Actions for the current user. <paramref name="pendingEdit"/> is the deal's
    /// open edit proposal, if any (null = none): the proposer may cancel it, the
    /// other party may approve/reject it, and nobody may propose another while
    /// one is open. Cancel is available to either party before any money moves.
    /// </summary>
    public static IReadOnlyList<string> For(Deal deal, long userId, DateTime now, DealEditRequest? pendingEdit)
    {
        var actions = new List<string>();
        var isBuyer = userId == deal.BuyerId;
        var isSeller = userId == deal.SellerId;

        switch (deal.Status)
        {
            case DealStatus.PendingAcceptance:
                if (pendingEdit is null)
                {
                    if (isSeller)
                    {
                        actions.Add("accept");
                        actions.Add("reject");
                    }
                    actions.Add("propose_edit");
                }
                else if (pendingEdit.ProposedById == userId)
                {
                    actions.Add("cancel_edit");
                }
                else
                {
                    actions.Add("approve_edit");
                    actions.Add("reject_edit");
                }
                actions.Add("cancel");
                break;
            case DealStatus.AwaitingFunding:
                if (isBuyer) actions.Add("fund");
                actions.Add("cancel");
                break;
            case DealStatus.Funded when isSeller:
                actions.Add("deliver");
                break;
            case DealStatus.Inspection when isBuyer:
                actions.Add("release");
                if (deal.InspectionEndsAt is { } ends && now <= ends) actions.Add("dispute");
                break;
        }

        return actions;
    }

    public static string Category(DealStatus status) => status switch
    {
        DealStatus.PendingAcceptance or DealStatus.AwaitingFunding => DealCategory.New,
        DealStatus.Funded or DealStatus.InDelivery or DealStatus.Inspection
            or DealStatus.Disputed or DealStatus.RefundPending => DealCategory.InProgress,
        _ => DealCategory.Completed
    };

    public static DealStatus[] StatusesOf(string category) => category switch
    {
        DealCategory.New => [DealStatus.PendingAcceptance, DealStatus.AwaitingFunding],
        DealCategory.InProgress => [DealStatus.Funded, DealStatus.InDelivery, DealStatus.Inspection, DealStatus.Disputed, DealStatus.RefundPending],
        _ => [DealStatus.Released, DealStatus.Rejected, DealStatus.Cancelled, DealStatus.Expired, DealStatus.Refunded]
    };
}

public static class UserLookup
{
    /// <summary>Finds an active user by Saudi mobile (05xxxxxxxx / +9665xxxxxxxx) or by public user code.</summary>
    public static async Task<User?> FindByPhoneOrCodeAsync(IApplicationDbContext db, string phoneOrCode, CancellationToken ct)
    {
        var query = phoneOrCode.Trim();
        if (query.Length == 0) return null;

        if (Etamid.Domain.Identity.ValueObjects.PhoneNumber.TryParse(query, out var phone))
        {
            var e164 = phone.E164;
            return await db.Users.FirstOrDefaultAsync(
                u => u.PhoneNormalized == e164 && u.Status == AccountStatus.Active && u.DeletedAt == null, ct);
        }

        return await db.Users.FirstOrDefaultAsync(
            u => u.Code == query && u.Status == AccountStatus.Active && u.DeletedAt == null, ct);
    }
}

/// <summary>Enum values go out in the API's snake_case form (pending_acceptance), like every other enum.</summary>
public static class ApiNames
{
    public static string Of(Enum value) => Snake(value.ToString());

    public static string Snake(string pascal) => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(pascal);
}

public static class Hashing
{
    public static string Sha256Hex(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
