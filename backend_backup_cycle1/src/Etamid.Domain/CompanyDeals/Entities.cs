using Etamid.Domain.Common;
using Etamid.Domain.Deals;
using Etamid.SharedKernel;

namespace Etamid.Domain.CompanyDeals;

public enum CompanyDealStatus { Draft, Pending, Active, Completed, Rejected, Cancelled }
public enum InstallmentStatus { Scheduled, AwaitingApproval, Approved, Rejected, Paid, Overdue, PenaltyApplied }
public enum ApprovalDecision { Approved, Rejected }
public enum PenaltyStatus { Applied, Waived, Paid }

/// <summary>Serves both the wizard draft (Status = Draft) and the real deal.</summary>
public sealed class CompanyDeal : AuditableEntity<long>
{
    public long Number { get; set; }
    public long CreatorUserId { get; set; }
    public long? CounterpartyUserId { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public string CounterpartyPhone { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "SAR";
    public DateOnly StartDate { get; set; }
    public long? ContractFileId { get; set; }
    public int? ApprovalSendDaysBefore { get; set; }
    public int? ApprovalResponseDays { get; set; }
    public string? ApprovalNotes { get; set; }
    public decimal? PenaltyPercent { get; set; }
    public int PenaltyGraceDays { get; set; } = 30;
    public string? PenaltyClauses { get; set; }
    public byte WizardStep { get; set; }
    public CompanyDealStatus Status { get; set; }
    public long? RejectReasonId { get; set; }
    public string? RejectNote { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CompanyDealStatusHistory : CreatedEntity<long>
{
    public long CompanyDealId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public long? ActorUserId { get; set; }
    public ActorType ActorType { get; set; }
    public string? Note { get; set; }
}

public sealed class PaymentInstallment : AuditableEntity<long>
{
    public long CompanyDealId { get; set; }
    public byte Order { get; set; }
    public string? Label { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public InstallmentStatus Status { get; set; }
    public DateTime? ApprovalRequestedAt { get; set; }
    public DateTime? ApprovalDeadlineAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public long? PaymentId { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class InstallmentApproval : Entity<long>
{
    public long InstallmentId { get; set; }
    public long DecidedByUserId { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Reason { get; set; }
    public DateTime DecidedAt { get; set; }
}

public sealed class PenaltyCharge : Entity<long>
{
    public long InstallmentId { get; set; }
    public long CompanyDealId { get; set; }
    public decimal PenaltyPercent { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public int DaysLate { get; set; }
    public DateTime AppliedAt { get; set; }
    public long? LedgerTransactionId { get; set; }
    public PenaltyStatus Status { get; set; }
    public long? WaivedByAdminId { get; set; }
    public string? WaiverReason { get; set; }
}
