using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Deals;

public enum DealStatus
{
    PendingAcceptance,
    AwaitingFunding,
    Funded,
    InDelivery,
    Inspection,
    Released,
    Rejected,
    Cancelled,
    Expired,
    Disputed,
    RefundPending,
    Refunded
}

/// <summary>The immutable digital contract frozen when the seller accepts.</summary>
public sealed record ContractTerms(
    int Version, long DealId, long Number, long BuyerId, long SellerId,
    string Title, string Description, decimal Amount, string Currency,
    int DurationValue, string DurationUnit, bool NeedShipping,
    long? ShippingCompanyId, long? ShippingAddressId,
    decimal FeeAmount, decimal SellerNetAmount, DateTime AcceptedAt);

/// <summary>
/// The deal aggregate. Every state change goes through a method that checks who is acting and the
/// state machine, so a buyer can never release to themselves and a seller can never confirm receipt.
/// Commercial terms have private setters: after acceptance nothing can change them (see
/// <see cref="ContractTerms"/>).
/// </summary>
public sealed class Deal : AuditableEntity<long>
{
    private static readonly JsonSerializerOptions SnapshotJson = new() { WriteIndented = false };

    private Deal() { }

    public long Number { get; private set; }
    public long BuyerId { get; private set; }
    public long SellerId { get; private set; }
    /// <summary>The party who created the deal (the buyer in the current contract).</summary>
    public long CreatedById { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public string Currency { get; private set; } = "SAR";
    public int DurationValue { get; private set; }
    public DurationUnit DurationUnit { get; private set; }
    public DealStatus Status { get; private set; }
    public bool NeedShipping { get; private set; }
    public long? ShippingCompanyId { get; private set; }
    public long? ShippingAddressId { get; private set; }
    public string? ShippingAddressSnapshot { get; private set; }
    public string? PaymentMethodCode { get; private set; }
    public long? ImageFileId { get; private set; }
    public long? RejectReasonId { get; private set; }
    public string? RejectNote { get; private set; }
    public DateTime RespondDeadlineAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? FundedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    /// <summary>While unfunded: the moment the deal lapses (response deadline, then funding deadline).</summary>
    public DateTime? ExpiresAt { get; private set; }
    /// <summary>Platform fee (fixed, charged to the seller; see AppSettings platform_fee_amount).</summary>
    public decimal? FeeAmount { get; private set; }
    public decimal? SellerNetAmount { get; private set; }
    public bool IsDisputed { get; private set; }
    public DateTime? DisputedAt { get; private set; }
    public DateTime? InspectionEndsAt { get; private set; }
    /// <summary>JSON of the frozen contract; null until accepted.</summary>
    public string? ContractSnapshot { get; private set; }
    /// <summary>SHA-256 (hex) of <see cref="ContractSnapshot"/>.</summary>
    public string? ContractHash { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Audit trail of every status move; new rows are saved with the deal in the same unit of work.</summary>
    public List<DealStatusHistory> History { get; } = [];

    public TimeSpan InspectionDuration
        => DurationUnit == DurationUnit.Hour ? TimeSpan.FromHours(DurationValue) : TimeSpan.FromDays(DurationValue);

    public bool IsParticipant(long userId) => userId == BuyerId || userId == SellerId;

    // ---------------------------------------------------------------- creation
    public static Result<Deal> Create(
        long buyerId, long sellerId, string title, string description, decimal amount,
        int durationValue, DurationUnit durationUnit, bool needShipping,
        long? shippingCompanyId, long? shippingAddressId, long? imageFileId,
        string currency, DateTime now, TimeSpan respondWindow)
    {
        if (buyerId == sellerId) return DealErrors.SameParties;
        if (amount <= 0 || decimal.Round(amount, 2) != amount) return DealErrors.AmountInvalid;
        if (durationValue <= 0) return DealErrors.DurationInvalid;

        var deal = new Deal
        {
            BuyerId = buyerId,
            SellerId = sellerId,
            CreatedById = buyerId,
            Title = title.Trim(),
            Description = description.Trim(),
            Amount = amount,
            OriginalAmount = amount,
            Currency = currency,
            DurationValue = durationValue,
            DurationUnit = durationUnit,
            NeedShipping = needShipping,
            ShippingCompanyId = needShipping ? shippingCompanyId : null,
            ShippingAddressId = needShipping ? shippingAddressId : null,
            ImageFileId = imageFileId,
            Status = DealStatus.PendingAcceptance,
            RespondDeadlineAt = now + respondWindow,
            ExpiresAt = now + respondWindow
        };
        deal.History.Add(new DealStatusHistory
        {
            FromStatus = null,
            ToStatus = DealStatus.PendingAcceptance.ToString(),
            ActorUserId = buyerId,
            ActorType = ActorType.Buyer
        });
        return deal;
    }

    // ---------------------------------------------------------------- seller answers
    public Result Accept(long actorId, decimal feeAmount, DateTime now, TimeSpan fundingWindow)
    {
        if (actorId != SellerId) return DealErrors.NotAllowed;
        if (Status != DealStatus.PendingAcceptance) return DealErrors.InvalidState;
        if (now > RespondDeadlineAt) return DealErrors.ResponseExpired;
        if (feeAmount < 0 || feeAmount >= Amount) return DealErrors.AmountTooLow;

        FeeAmount = feeAmount;
        SellerNetAmount = Amount - feeAmount;
        AcceptedAt = now;
        ExpiresAt = now + fundingWindow;
        SealContract(now);
        Move(DealStatus.AwaitingFunding, actorId, ActorType.Seller, null);
        return Result.Success();
    }

    public Result Reject(long actorId, long? reasonId, string? note, DateTime now)
    {
        if (actorId != SellerId) return DealErrors.NotAllowed;
        if (Status != DealStatus.PendingAcceptance) return DealErrors.InvalidState;

        RejectReasonId = reasonId;
        RejectNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ExpiresAt = null;
        Move(DealStatus.Rejected, actorId, ActorType.Seller, RejectNote);
        return Result.Success();
    }

    /// <summary>Either party may withdraw before any money moves.</summary>
    public Result Cancel(long actorId, DateTime now)
    {
        if (!IsParticipant(actorId)) return DealErrors.NotAllowed;
        if (Status is not (DealStatus.PendingAcceptance or DealStatus.AwaitingFunding)) return DealErrors.InvalidState;

        ExpiresAt = null;
        Move(DealStatus.Cancelled, actorId, actorId == BuyerId ? ActorType.Buyer : ActorType.Seller, null);
        return Result.Success();
    }

    /// <summary>
    /// Applies an approved edit. Edits are only possible before acceptance, because
    /// acceptance freezes the terms into the contract snapshot + hash; changing terms
    /// afterwards would break <see cref="IsContractIntact"/>. Fee/SellerNet are still
    /// null at this stage, so nothing needs recomputing. OriginalAmount is kept as
    /// the amount the deal was created with, for audit.
    /// </summary>
    public Result ApplyEdit(decimal? amount, int? durationValue, DurationUnit? durationUnit, string? description)
    {
        if (Status != DealStatus.PendingAcceptance) return DealErrors.InvalidState;
        if (amount is { } a && (a <= 0 || decimal.Round(a, 2) != a)) return DealErrors.AmountInvalid;
        if (durationValue is { } dv && dv <= 0) return DealErrors.DurationInvalid;

        if (amount is { } newAmount) Amount = newAmount;
        if (durationValue is { } newDuration) DurationValue = newDuration;
        if (durationUnit is { } newUnit) DurationUnit = newUnit;
        if (description is not null) Description = description.Trim();
        return Result.Success();
    }

    /// <summary>System sweep for deals nobody answered / funded in time.</summary>
    public Result Expire(DateTime now)
    {
        var unfunded = Status is DealStatus.PendingAcceptance or DealStatus.AwaitingFunding;
        if (!unfunded || ExpiresAt is not { } at || now <= at) return DealErrors.InvalidState;

        ExpiresAt = null;
        Move(DealStatus.Expired, null, ActorType.System, null);
        return Result.Success();
    }

    // ---------------------------------------------------------------- money in (system, after a trusted webhook)
    public Result MarkFunded(DateTime now)
    {
        if (Status != DealStatus.AwaitingFunding) return DealErrors.InvalidState;

        FundedAt = now;
        ExpiresAt = null;
        Move(DealStatus.Funded, null, ActorType.System, null);
        return Result.Success();
    }

    // ---------------------------------------------------------------- delivery and inspection
    /// <summary>Seller hands over: Funded → InDelivery → Inspection, and the inspection clock starts.</summary>
    public Result Deliver(long actorId, DateTime now)
    {
        if (actorId != SellerId) return DealErrors.NotAllowed;
        if (Status != DealStatus.Funded) return DealErrors.InvalidState;

        ShippedAt = now;
        Move(DealStatus.InDelivery, actorId, ActorType.Seller, null);

        DeliveredAt = now;
        InspectionEndsAt = now + InspectionDuration;
        Move(DealStatus.Inspection, actorId, ActorType.Seller, null);
        return Result.Success();
    }

    /// <summary>The buyer confirms receipt: only the buyer can do this.</summary>
    public Result ConfirmReceipt(long actorId, DateTime now)
    {
        if (actorId != BuyerId) return DealErrors.NotAllowed;
        if (IsDisputed || Status == DealStatus.Disputed) return DealErrors.Disputed;
        if (Status != DealStatus.Inspection) return DealErrors.InvalidState;

        CompletedAt = now;
        Move(DealStatus.Released, actorId, ActorType.Buyer, null);
        return Result.Success();
    }

    /// <summary>System release once the inspection window passed with no dispute.</summary>
    public Result AutoRelease(DateTime now)
    {
        if (IsDisputed || Status == DealStatus.Disputed) return DealErrors.Disputed;
        if (Status != DealStatus.Inspection) return DealErrors.InvalidState;
        if (InspectionEndsAt is not { } ends || now < ends) return DealErrors.InspectionNotEnded;

        CompletedAt = now;
        Move(DealStatus.Released, null, ActorType.System, null);
        return Result.Success();
    }

    /// <summary>Opens the dispute path; from this moment nothing may release the money automatically.</summary>
    public Result OpenDispute(long actorId, DateTime now)
    {
        if (actorId != BuyerId) return DealErrors.NotAllowed;
        if (IsDisputed || Status == DealStatus.Disputed) return DealErrors.Disputed;
        if (Status != DealStatus.Inspection) return DealErrors.InvalidState;
        if (InspectionEndsAt is not { } ends || now > ends) return DealErrors.InspectionEnded;

        IsDisputed = true;
        DisputedAt = now;
        Move(DealStatus.Disputed, actorId, ActorType.Buyer, null);
        return Result.Success();
    }

    // ---------------------------------------------------------------- contract integrity
    /// <summary>True when the stored snapshot still hashes to <see cref="ContractHash"/> and the live terms still equal it.</summary>
    public bool IsContractIntact()
    {
        if (ContractSnapshot is null || ContractHash is null) return false;
        if (!string.Equals(Hash(ContractSnapshot), ContractHash, StringComparison.Ordinal)) return false;

        var s = JsonSerializer.Deserialize<ContractTerms>(ContractSnapshot, SnapshotJson);
        return s is not null
               && s.DealId == Id && s.BuyerId == BuyerId && s.SellerId == SellerId
               && s.Amount == Amount && s.Currency == Currency
               && s.FeeAmount == FeeAmount && s.SellerNetAmount == SellerNetAmount
               && s.DurationValue == DurationValue && s.DurationUnit == DurationUnit.ToString()
               && s.Title == Title && s.Description == Description && s.NeedShipping == NeedShipping;
    }

    private void SealContract(DateTime now)
    {
        var snapshot = new ContractTerms(
            1, Id, Number, BuyerId, SellerId, Title, Description, Amount, Currency,
            DurationValue, DurationUnit.ToString(), NeedShipping, ShippingCompanyId, ShippingAddressId,
            FeeAmount!.Value, SellerNetAmount!.Value, now);

        ContractSnapshot = JsonSerializer.Serialize(snapshot, SnapshotJson);
        ContractHash = Hash(ContractSnapshot);
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private void Move(DealStatus to, long? actorId, ActorType actorType, string? note)
    {
        if (!DealStateMachine.CanMove(Status, to))
            throw new InvalidOperationException($"Illegal deal transition {Status} -> {to}.");

        var from = Status;
        Status = to;
        History.Add(new DealStatusHistory
        {
            FromStatus = from.ToString(),
            ToStatus = to.ToString(),
            ActorUserId = actorId,
            ActorType = actorType,
            Note = note
        });
    }
}
