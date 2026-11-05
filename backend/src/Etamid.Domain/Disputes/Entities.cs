using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Disputes;

public enum DisputeStatus { UnderReview, AwaitingInfo, ResolvedBuyer, ResolvedSeller, Closed }
public enum DisputeResolution { None, RefundBuyer, ReleaseSeller, Partial }
public enum DisputeSenderType { Buyer, Seller, Admin }
public enum SettlementProposalStatus { Open, Accepted, Rejected }

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
    /// <summary>Deadline the operator set when requesting more information; null unless info was requested.</summary>
    public DateTime? InfoDeadlineAt { get; set; }
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

/// <summary>
/// One party's offer to end the dispute without an admin decision. Only one may be
/// open at a time; accepting it executes the money movement immediately, rejecting
/// it leaves the dispute open for another proposal or an admin decision.
/// </summary>
public sealed class DisputeSettlementProposal : AuditableEntity<long>
{
    public long DisputeId { get; set; }
    public long ProposedByUserId { get; set; }
    public DisputeResolution Resolution { get; set; }
    /// <summary>Seller's percent of the deal amount; required only when Resolution is Partial (1..99).</summary>
    public int? SellerPercent { get; set; }
    public SettlementProposalStatus Status { get; set; }
    public DateTime? DecidedAt { get; set; }
    public long? DecidedByUserId { get; set; }
}
