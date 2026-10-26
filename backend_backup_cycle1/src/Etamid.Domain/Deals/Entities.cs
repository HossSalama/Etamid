using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Deals;

public enum DealStatus
{
    Draft, PendingSeller, EditProposed, PendingBuyerApproval, Accepted, Funded,
    Shipped, Delivered, Completed, Rejected, Disputed, Refunded, Expired, Cancelled
}
public enum DurationUnit { Hour, Day }
public enum EditStatus { Pending, Approved, Rejected, Cancelled }
public enum ActorType { Buyer, Seller, System, Admin }
public enum ShipmentStatus { Created, PickedUp, InTransit, OutForDelivery, Delivered, Failed, Returned }

public sealed class Deal : AuditableEntity<long>
{
    public long Number { get; set; }
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal OriginalAmount { get; set; }
    public string Currency { get; set; } = "SAR";
    public int DurationValue { get; set; }
    public DurationUnit DurationUnit { get; set; }
    public DealStatus Status { get; set; }
    public bool NeedShipping { get; set; }
    public long? ShippingCompanyId { get; set; }
    public long? ShippingAddressId { get; set; }
    public string? ShippingAddressSnapshot { get; set; }
    public string? PaymentMethodCode { get; set; }
    public long? ImageFileId { get; set; }
    public long? RejectReasonId { get; set; }
    public string? RejectNote { get; set; }
    public DateTime RespondDeadlineAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? FundedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    /// <summary>Platform fee (currently a fixed 50 SAR charged to the seller; see AppSettings).</summary>
    public decimal? FeeAmount { get; set; }
    public decimal? SellerNetAmount { get; set; }
    public bool IsDisputed { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class DealEditRequest : CreatedEntity<long>
{
    public long DealId { get; set; }
    public long ProposedById { get; set; }
    public decimal PrevAmount { get; set; }
    public int PrevDurationValue { get; set; }
    public string PrevDurationUnit { get; set; } = string.Empty;
    public string PrevDescription { get; set; } = string.Empty;
    public decimal? NewAmount { get; set; }
    public int? NewDurationValue { get; set; }
    public string? NewDurationUnit { get; set; }
    public string? NewDescription { get; set; }
    public EditStatus Status { get; set; }
    public DateTime? RespondedAt { get; set; }
    public long? RespondedById { get; set; }
}

public sealed class DealStatusHistory : CreatedEntity<long>
{
    public long DealId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public long? ActorUserId { get; set; }
    public ActorType ActorType { get; set; }
    public string? Note { get; set; }
}

public sealed class Shipment : AuditableEntity<long>
{
    public long DealId { get; set; }
    public long ShippingCompanyId { get; set; }
    public string? TrackingNumber { get; set; }
    public long? WaybillFileId { get; set; }
    public ShipmentStatus Status { get; set; }
    public string? ProviderPayload { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

public sealed class ShipmentEvent : Entity<long>
{
    public long ShipmentId { get; set; }
    public DateTime EventAt { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public sealed class Rating : CreatedEntity<long>
{
    public long DealId { get; set; }
    public long RaterId { get; set; }
    public long SellerId { get; set; }
    public byte Stars { get; set; }
    public string? Comment { get; set; }
    public bool IsVisible { get; set; } = true;
}
