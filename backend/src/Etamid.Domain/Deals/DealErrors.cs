using Etamid.SharedKernel;

namespace Etamid.Domain.Deals;

/// <summary>Stable error codes of the deal / escrow cycle (each one has an ar + en message).</summary>
public static class DealErrors
{
    public static readonly Error NotFound = Error.NotFound("DEAL_NOT_FOUND");
    public static readonly Error SellerNotFound = Error.NotFound("SELLER_NOT_FOUND", "sellerPhoneOrCode");
    public static readonly Error PaymentNotFound = Error.NotFound("PAYMENT_NOT_FOUND");

    public static readonly Error InvalidState = Error.Conflict("DEAL_INVALID_STATE");
    public static readonly Error NotAllowed = Error.Forbidden("DEAL_ACTION_NOT_ALLOWED");
    public static readonly Error Disputed = Error.Conflict("DEAL_DISPUTED");
    public static readonly Error ResponseExpired = Error.Conflict("DEAL_RESPONSE_EXPIRED");
    public static readonly Error FundingExpired = Error.Conflict("DEAL_FUNDING_EXPIRED");
    public static readonly Error InspectionNotEnded = Error.Conflict("DEAL_INSPECTION_NOT_ENDED");
    public static readonly Error InspectionEnded = Error.Conflict("DEAL_INSPECTION_ENDED");
    public static readonly Error ContractTampered = Error.Failure("DEAL_CONTRACT_TAMPERED");
    public static readonly Error EscrowNotHeld = Error.Conflict("ESCROW_NOT_HELD");

    public static readonly Error SameParties = Error.Validation("DEAL_SAME_PARTIES", "sellerPhoneOrCode");
    public static readonly Error AmountInvalid = Error.Validation("DEAL_AMOUNT_INVALID", "amount");
    public static readonly Error AmountTooLow = Error.Validation("DEAL_AMOUNT_TOO_LOW", "amount");
    public static readonly Error DurationInvalid = Error.Validation("INVALID_VALUE", "durationValue");

    public static readonly Error IdempotencyKeyRequired = Error.Validation("IDEMPOTENCY_KEY_REQUIRED", "X-Idempotency-Key");
    public static readonly Error IdempotencyKeyReused = Error.Conflict("IDEMPOTENCY_KEY_REUSED");

    public static readonly Error WebhookSignatureInvalid = Error.Unauthorized("WEBHOOK_SIGNATURE_INVALID");
    public static readonly Error WebhookPayloadInvalid = Error.Validation("WEBHOOK_PAYLOAD_INVALID");

    public static readonly Error LedgerUnbalanced = Error.Failure("LEDGER_UNBALANCED");
}
