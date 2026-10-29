namespace Etamid.Application.Deals;

public sealed record DealSummaryDto(
    long Id, long Number, string Title, decimal Amount, string Currency, string Status,
    string Role, string CounterpartyName, string CounterpartyCode, DateTime CreatedAt,
    IReadOnlyList<string> AvailableActions);

public sealed record DealTimelineItemDto(string? From, string To, string ActorType, DateTime At, string? Note);

/// <summary>A single pending edit proposal. New* is null when that field was not proposed to change.</summary>
public sealed record DealEditDto(
    long Id, long DealId, string ProposedBy, string Status,
    decimal PrevAmount, int PrevDurationValue, string PrevDurationUnit, string PrevDescription,
    decimal? NewAmount, int? NewDurationValue, string? NewDurationUnit, string? NewDescription,
    DateTime CreatedAt, DateTime? RespondedAt);

/// <summary>
/// Seller preview for GET /users/lookup. Deliberately minimal: no user id, no phone
/// and no email — the caller already typed the phone/code, and only needs to confirm
/// who they are about to deal with. Verified means the email address is verified.
/// </summary>
public sealed record CounterpartyDto(
    string Name, string Code, string? StoreName, string? AvatarUrl,
    bool EmailVerified, bool NafathVerified, decimal Rating, int SuccessfulDeals);

public sealed record DealDetailDto(
    long Id, long Number, string Title, string Description, decimal Amount, string Currency, string Status,
    string Role, string CounterpartyName, string CounterpartyCode,
    int DurationValue, string DurationUnit, bool NeedShipping, long? ShippingCompanyId, long? ShippingAddressId,
    decimal? FeeAmount, decimal? SellerNetAmount, bool IsDisputed,
    DateTime CreatedAt, DateTime RespondDeadlineAt, DateTime? AcceptedAt, DateTime? FundedAt, DateTime? DeliveredAt,
    DateTime? InspectionEndsAt, DateTime? CompletedAt, DateTime? ExpiresAt,
    string? ContractHash, DealEditDto? PendingEdit,
    IReadOnlyList<string> AvailableActions, IReadOnlyList<DealTimelineItemDto> Timeline);

public sealed record FundSessionDto(
    Guid PaymentId, long DealId, string Status, decimal Amount, string Currency,
    string Provider, string ProviderPaymentId, string RedirectUrl);

public sealed record ReleaseResultDto(
    long DealId, string Status, decimal Amount, decimal FeeAmount, decimal SellerNetAmount);

public sealed record DisputeDto(long Id, string Number, long DealId, string Status);

public sealed record WebhookResultDto(string Outcome);

public static class DealCategory
{
    public const string New = "new";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
}
