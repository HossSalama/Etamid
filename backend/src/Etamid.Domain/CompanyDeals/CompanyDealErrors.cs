using Etamid.SharedKernel;

namespace Etamid.Domain.CompanyDeals;

/// <summary>Stable error codes of the company-deal cycle (each one has an ar + en message).</summary>
public static class CompanyDealErrors
{
    public static readonly Error NotFound = Error.NotFound("COMPANY_DEAL_NOT_FOUND");
    public static readonly Error InstallmentNotFound = Error.NotFound("COMPANY_INSTALLMENT_NOT_FOUND");
    public static readonly Error PenaltyNotFound = Error.NotFound("COMPANY_PENALTY_NOT_FOUND");

    public static readonly Error InvalidState = Error.Conflict("COMPANY_DEAL_INVALID_STATE");
    public static readonly Error NotAllowed = Error.Forbidden("COMPANY_DEAL_ACTION_NOT_ALLOWED");
    public static readonly Error EscrowNotFunded = Error.Conflict("COMPANY_ESCROW_NOT_FUNDED");

    public static readonly Error CounterpartyNotFound = Error.NotFound("COUNTERPARTY_NOT_FOUND", "counterpartyPhoneOrCode");
    public static readonly Error SameParties = Error.Validation("COMPANY_DEAL_SAME_PARTIES", "counterpartyPhoneOrCode");
    public static readonly Error SumMismatch = Error.Validation("COMPANY_INSTALLMENTS_SUM_MISMATCH", "installments");
}
