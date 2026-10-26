using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Disputes;

public enum DisputeStatus { UnderReview, AwaitingInfo, ResolvedBuyer, ResolvedSeller, Closed }
public enum DisputeResolution { None, RefundBuyer, ReleaseSeller, Partial }
public enum DisputeSenderType { Buyer, Seller, Admin }

public sealed class Dispute : AuditableEntity<long>
{
    public string Number { get; set; } = string.Empty;
    public long DealId { get; set; }
    public long OpenedById { get; set; }
    public long AgainstUserId { get; set; }
    public long ReasonId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public DisputeResolution Resolution { get; set; }
    public string? ResolutionNote { get; set; }
    public long? ResolvedByAdminId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public long? AssignedAdminId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class DisputeMessage : CreatedEntity<long>
{
    public long DisputeId { get; set; }
    public DisputeSenderType SenderType { get; set; }
    public long? SenderUserId { get; set; }
    public long? SenderAdminId { get; set; }
    public string? Message { get; set; }
    public bool IsInternalNote { get; set; }
}

public sealed class DisputeAttachment : CreatedEntity<long>
{
    public long DisputeId { get; set; }
    public long? MessageId { get; set; }
    public long FileId { get; set; }
    public long? UploadedByUserId { get; set; }
}
