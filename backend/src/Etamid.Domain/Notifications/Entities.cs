using Etamid.SharedKernel;
using Etamid.Domain.Common;

namespace Etamid.Domain.Notifications;

public enum NotificationType
{
    DealRequest, DealAccepted, DealRejected, DealEdit, DealFunded, DealShipped,
    DealDelivered, DisputeUpdate, PaymentApproval, PaymentDue, Penalty, Wallet, System
}
public enum NotificationChannel { Push, Sms, InApp }
public enum CampaignStatus { Draft, Scheduled, Sending, Sent, Cancelled }

public sealed class Notification : CreatedEntity<long>
{
    public long UserId { get; set; }
    public NotificationType Type { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public long? DealId { get; set; }
    public long? CompanyDealId { get; set; }
    public long? InstallmentId { get; set; }
    public long? DisputeId { get; set; }
    public string? Data { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? PushSentAt { get; set; }
}

public sealed class NotificationTemplate : Entity<long>
{
    public string Code { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class BroadcastCampaign : CreatedEntity<long>
{
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string BodyEn { get; set; } = string.Empty;
    public string? AudienceFilter { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int RecipientsCount { get; set; }
    public long CreatedByAdminId { get; set; }
}
