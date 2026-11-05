using Etamid.SharedKernel;

namespace Etamid.Domain.Disputes;

/// <summary>Stable error codes of the dispute cycle (each one has an ar + en message).</summary>
public static class DisputeErrors
{
    public static readonly Error NotFound = Error.NotFound("DISPUTE_NOT_FOUND");
    public static readonly Error ProposalNotFound = Error.NotFound("SETTLEMENT_PROPOSAL_NOT_FOUND");

    public static readonly Error InvalidState = Error.Conflict("DISPUTE_INVALID_STATE");
    public static readonly Error NotAllowed = Error.Forbidden("DISPUTE_ACTION_NOT_ALLOWED");
    public static readonly Error NotOperator = Error.Forbidden("DISPUTE_NOT_OPERATOR");
    public static readonly Error AlreadyResolved = Error.Conflict("DISPUTE_ALREADY_RESOLVED");
    public static readonly Error ProposalAlreadyOpen = Error.Conflict("SETTLEMENT_ALREADY_OPEN");
    public static readonly Error ProposalNotOpen = Error.Conflict("SETTLEMENT_PROPOSAL_NOT_OPEN");
}
