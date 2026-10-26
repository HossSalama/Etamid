using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Money;

public enum LedgerAccountType { UserWallet, PlatformEscrow, PlatformRevenue, GatewayClearing, BankPayout, PenaltyIncome }
public enum LedgerTransactionType { EscrowFunding, EscrowRelease, EscrowRefund, Withdrawal, Fee, Penalty, Adjustment }
public enum LedgerDirection { Debit, Credit }
public enum CreatedByType { User, System, Admin }
public enum EscrowStatus { Held, Released, Refunded, FrozenDispute }
public enum ReleaseReason { BuyerConfirmed, AutoRelease, DisputeSeller, DisputeBuyer }
/// <summary>Only BankTransfer is enabled for now (client decision); others stay for later.</summary>
public enum PaymentMethod { BankTransfer, ApplePay, Mada, Card, Wallet }
public enum PaymentStatus { Initiated, RequiresAction, Succeeded, Failed, Refunded, PartiallyRefunded }
public enum WebhookStatus { Received, Processed, Failed, Ignored }
public enum WithdrawalStatus { Pending, Approved, Processing, Completed, Rejected, Failed }

public sealed class Wallet : AuditableEntity<long>
{
    public long UserId { get; set; }
    public long LedgerAccountId { get; set; }
    public bool Activated { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public decimal AvailableBalance { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class LedgerAccount : Entity<long>
{
    public LedgerAccountType Type { get; set; }
    public long? OwnerUserId { get; set; }
    public string Currency { get; set; } = "SAR";
}

public sealed class LedgerTransaction : CreatedEntity<long>
{
    public Guid PublicId { get; set; }
    public LedgerTransactionType Type { get; set; }
    public long? DealId { get; set; }
    public long? CompanyDealId { get; set; }
    public long? InstallmentId { get; set; }
    public long? WithdrawalId { get; set; }
    public long? PaymentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public CreatedByType CreatedByType { get; set; }
    public long? CreatedById { get; set; }
}

/// <summary>Append-only. Never updated or deleted; corrections are new reversing entries.</summary>
public sealed class LedgerEntry : CreatedEntity<long>
{
    public long TransactionId { get; set; }
    public long AccountId { get; set; }
    public LedgerDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal? BalanceAfter { get; set; }
}

public sealed class EscrowHold : Entity<long>
{
    public long DealId { get; set; }
    public long BuyerId { get; set; }
    public long SellerId { get; set; }
    public decimal Amount { get; set; }
    public decimal? FeeAmount { get; set; }
    public EscrowStatus Status { get; set; }
    public long PaymentId { get; set; }
    public DateTime HeldAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public ReleaseReason? ReleaseReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class Payment : AuditableEntity<long>
{
    public Guid PublicId { get; set; }
    public long UserId { get; set; }
    public long? DealId { get; set; }
    public long? InstallmentId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";
    public PaymentStatus Status { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public string? ProviderPayload { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    // Bank-transfer fields (client flow still to be confirmed)
    public string? TransferReference { get; set; }
    public long? ProofFileId { get; set; }
    public long? ConfirmedByAdminId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

public sealed class PaymentWebhookEvent : Entity<long>
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public bool SignatureValid { get; set; }
    public WebhookStatus Status { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public sealed class WithdrawalRequest : CreatedEntity<long>
{
    public long UserId { get; set; }
    public long WalletId { get; set; }
    public long BankAccountId { get; set; }
    public string IbanSnapshotMasked { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal FeeAmount { get; set; }
    public WithdrawalStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public long? ApprovedByAdminId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? BankReference { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

/// <summary>Table "IdempotencyKeys". Composite key (UserId, Key).</summary>
public sealed class IdempotencyRecord
{
    public long UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
