using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Application.Notifications;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Money;
using Etamid.Domain.Notifications;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Disputes;

// ---------- POST /deals/{id}/dispute/settlement/propose ----------

public sealed record ProposeSettlementCommand(long DealId, string Resolution, int? SellerPercent = null)
    : IRequest<Result<SettlementProposalDto>>;

public sealed class ProposeSettlementValidator : AbstractValidator<ProposeSettlementCommand>
{
    public ProposeSettlementValidator()
    {
        RuleFor(x => x.Resolution).MustBeRequired(40);
        RuleFor(x => x.SellerPercent).InclusiveBetween(1, 99).When(x => x.SellerPercent is not null);
    }
}

public sealed class ProposeSettlementHandler(IApplicationDbContext db, ICurrentUser currentUser, NotificationService notifications)
    : IRequestHandler<ProposeSettlementCommand, Result<SettlementProposalDto>>
{
    public async Task<Result<SettlementProposalDto>> Handle(ProposeSettlementCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        if (!Enum.TryParse<DisputeResolution>(r.Resolution, ignoreCase: true, out var resolution)
            || resolution is DisputeResolution.None)
            return Error.Validation("INVALID_VALUE", "resolution");
        if (resolution == DisputeResolution.Partial && r.SellerPercent is null)
            return Error.Validation("INVALID_VALUE", "sellerPercent");
        if (resolution != DisputeResolution.Partial && r.SellerPercent is not null)
            return Error.Validation("INVALID_VALUE", "sellerPercent");

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        var dispute = await db.Disputes
            .Where(d => d.DealId == deal.Id).OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (dispute is null) return DisputeErrors.NotFound;
        if (!DisputeMapper.IsOpen(dispute.Status)) return DisputeErrors.InvalidState;

        var alreadyOpen = await db.DisputeSettlementProposals
            .AnyAsync(p => p.DisputeId == dispute.Id && p.Status == SettlementProposalStatus.Open, ct);
        if (alreadyOpen) return DisputeErrors.ProposalAlreadyOpen;

        var proposal = new DisputeSettlementProposal
        {
            DisputeId = dispute.Id,
            ProposedByUserId = userId,
            Resolution = resolution,
            SellerPercent = r.SellerPercent,
            Status = SettlementProposalStatus.Open
        };
        db.DisputeSettlementProposals.Add(proposal);
        notifications.Create(userId == deal.BuyerId ? deal.SellerId : deal.BuyerId, NotificationType.DisputeUpdate,
            "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
            dealId: deal.Id, disputeId: dispute.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        var proposerName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        return new SettlementProposalDto(
            proposal.Id, proposal.ProposedByUserId, proposerName, ApiNames.Of(proposal.Resolution),
            proposal.SellerPercent, ApiNames.Of(proposal.Status), proposal.CreatedAt, proposal.DecidedAt);
    }
}

// ---------- POST /deals/{id}/dispute/settlement/{proposalId}/accept ----------

public sealed record AcceptSettlementCommand(long DealId, long ProposalId)
    : IRequest<Result<DisputeExecutionDto>>;

public sealed class AcceptSettlementHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, DisputeExecutionService execution,
    NotificationService notifications)
    : IRequestHandler<AcceptSettlementCommand, Result<DisputeExecutionDto>>
{
    public async Task<Result<DisputeExecutionDto>> Handle(AcceptSettlementCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var (deal, dispute, proposal) = await LoadProposalAsync(db, r.DealId, r.ProposalId, userId, ct);
        if (deal is null || dispute is null || proposal is null) return DealErrors.NotFound;
        if (proposal.Status != SettlementProposalStatus.Open) return DisputeErrors.ProposalNotOpen;
        if (proposal.ProposedByUserId == userId) return DisputeErrors.NotAllowed;

        // Accepting executes the money movement immediately, exactly like an admin decision.
        var executed = await execution.ExecuteAsync(
            deal, dispute, proposal.Resolution, proposal.SellerPercent, CreatedByType.User, ct);
        if (executed.IsFailure) return executed.Error;

        proposal.Status = SettlementProposalStatus.Accepted;
        proposal.DecidedAt = clock.UtcNow;
        proposal.DecidedByUserId = userId;

        notifications.NotifyDisputeDecision(deal, dispute.Id, executed.Value.SellerAmount, executed.Value.BuyerAmount);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return executed.Value;
    }

    internal static async Task<(Deal? Deal, Dispute? Dispute, DisputeSettlementProposal? Proposal)> LoadProposalAsync(
        IApplicationDbContext db, long dealId, long proposalId, long userId, CancellationToken ct)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == dealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return (null, null, null);

        var dispute = await db.Disputes
            .Where(d => d.DealId == deal.Id).OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (dispute is null || !DisputeMapper.IsOpen(dispute.Status)) return (deal, dispute, null);

        var proposal = await db.DisputeSettlementProposals
            .FirstOrDefaultAsync(p => p.Id == proposalId && p.DisputeId == dispute.Id, ct);
        if (proposal is null) return (deal, dispute, null);
        if (proposal.Status != SettlementProposalStatus.Open) return (deal, dispute, proposal);

        return (deal, dispute, proposal);
    }
}

// ---------- POST /deals/{id}/dispute/settlement/{proposalId}/reject ----------

public sealed record RejectSettlementCommand(long DealId, long ProposalId)
    : IRequest<Result<SettlementProposalDto>>;

public sealed class RejectSettlementHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, NotificationService notifications)
    : IRequestHandler<RejectSettlementCommand, Result<SettlementProposalDto>>
{
    public async Task<Result<SettlementProposalDto>> Handle(RejectSettlementCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var (deal, dispute, proposal) = await AcceptSettlementHandler.LoadProposalAsync(db, r.DealId, r.ProposalId, userId, ct);
        if (deal is null || dispute is null || proposal is null) return DealErrors.NotFound;
        if (proposal.ProposedByUserId == userId) return DisputeErrors.NotAllowed;
        if (proposal.Status != SettlementProposalStatus.Open) return DisputeErrors.ProposalNotOpen;

        proposal.Status = SettlementProposalStatus.Rejected;
        proposal.DecidedAt = clock.UtcNow;
        proposal.DecidedByUserId = userId;

        notifications.Create(proposal.ProposedByUserId, NotificationType.DisputeUpdate,
            "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
            dealId: deal.Id, disputeId: dispute.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        var proposerName = await db.Users.AsNoTracking()
            .Where(u => u.Id == proposal.ProposedByUserId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        return new SettlementProposalDto(
            proposal.Id, proposal.ProposedByUserId, proposerName, ApiNames.Of(proposal.Resolution),
            proposal.SellerPercent, ApiNames.Of(proposal.Status), proposal.CreatedAt, proposal.DecidedAt);
    }
}
