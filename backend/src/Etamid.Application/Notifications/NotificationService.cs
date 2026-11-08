using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Deals;
using Etamid.Domain.Notifications;

namespace Etamid.Application.Notifications;

/// <summary>
/// In-app notifications (Layer 1 only — no Push, no SMS).
/// Rows are added to the same DbContext unit of work as the triggering event:
/// the caller saves, so a notification never exists without its event and an
/// event never commits without its notification. Titles and bodies are stored
/// in both languages, resolved from messages.ar/en.json at creation time.
/// </summary>
public sealed class NotificationService(IApplicationDbContext db, IMessageLocalizer localizer)
{
    public void Create(
        long userId, NotificationType type, string titleKey, string bodyKey, string label,
        long? dealId = null, long? companyDealId = null, long? installmentId = null, long? disputeId = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            TitleAr = Text(titleKey, "ar", label),
            TitleEn = Text(titleKey, "en", label),
            BodyAr = Text(bodyKey, "ar", label),
            BodyEn = Text(bodyKey, "en", label),
            DealId = dealId,
            CompanyDealId = companyDealId,
            InstallmentId = installmentId,
            DisputeId = disputeId
        });
    }

    /// <summary>
    /// A dispute decision or an accepted settlement: both parties get a dispute update,
    /// and whoever received money gets a wallet notification (release to the seller,
    /// refund to the buyer; a partial split notifies both).
    /// </summary>
    public void NotifyDisputeDecision(Deal deal, long disputeId, decimal sellerAmount, decimal buyerAmount)
    {
        Create(deal.BuyerId, NotificationType.DisputeUpdate,
            "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
            dealId: deal.Id, disputeId: disputeId);
        Create(deal.SellerId, NotificationType.DisputeUpdate,
            "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
            dealId: deal.Id, disputeId: disputeId);
        if (sellerAmount > 0m)
            Create(deal.SellerId, NotificationType.Wallet,
                "NOTIF_WALLET_RELEASE_TITLE", "NOTIF_WALLET_RELEASE_BODY",
                $"{sellerAmount} {deal.Currency} — {deal.Title}", dealId: deal.Id, disputeId: disputeId);
        if (buyerAmount > 0m)
            Create(deal.BuyerId, NotificationType.Wallet,
                "NOTIF_WALLET_REFUND_TITLE", "NOTIF_WALLET_REFUND_BODY",
                $"{buyerAmount} {deal.Currency} — {deal.Title}", dealId: deal.Id, disputeId: disputeId);
    }

    private string Text(string key, string language, string label)
        => localizer.Get(key, language).Replace("{0}", label, StringComparison.Ordinal);
}
