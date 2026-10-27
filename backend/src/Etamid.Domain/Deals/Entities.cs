using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Deals;

public enum DurationUnit { Hour, Day }
public enum EditStatus { Pending, Approved, Rejected, Cancelled }
public enum ActorType { Buyer, Seller, System, Admin }
public enum ShipmentStatus { Created, PickedUp, InTransit, OutForDelivery, Delivered, Failed, Returned }

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
