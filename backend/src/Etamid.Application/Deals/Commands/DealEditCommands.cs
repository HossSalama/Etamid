using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Deals;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Commands;

// ======================================================================= Cancel
/// <summary>Either party may cancel before any money moves (PendingAcceptance / AwaitingFunding only).</summary>
public sealed record CancelDealCommand(long DealId) : IRequest<Result<DealDetailDto>>;

public sealed class CancelDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<CancelDealCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(CancelDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        var now = clock.UtcNow;
        var cancelled = deal.Cancel(userId, now);
        if (cancelled.IsFailure) return cancelled.Error;

        // A pending edit dies with the deal; leave no open proposal on a terminal deal.
        var pending = await db.DealEditRequests
            .Where(e => e.DealId == deal.Id && e.Status == EditStatus.Pending)
            .ToListAsync(ct);
        foreach (var edit in pending)
        {
            edit.Status = EditStatus.Cancelled;
            edit.RespondedAt = now;
            edit.RespondedById = userId;
        }

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, now, ct);
    }
}

// ======================================================================= Edit: propose
/// <summary>
/// Propose changing amount, duration and/or description — before acceptance only.
/// Acceptance freezes the terms into the contract, so edits after that would break
/// the contract hash and are rejected. At most one edit may be pending per deal.
/// </summary>
public sealed record ProposeDealEditCommand(
    long DealId, decimal? Amount, int? DurationValue, string? DurationUnit, string? Description)
    : IRequest<Result<DealEditDto>>;

public sealed class ProposeDealEditValidator : AbstractValidator<ProposeDealEditCommand>
{
    public ProposeDealEditValidator()
    {
        RuleFor(x => x).Custom((x, ctx) =>
        {
            if (x.Amount is null && x.DurationValue is null && x.DurationUnit is null && x.Description is null)
                ctx.AddFailure("INVALID_VALUE", "Propose at least one change.");
        });
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithErrorCode("DEAL_AMOUNT_INVALID")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("DEAL_AMOUNT_INVALID")
            .Must(a => a is null || decimal.Round(a.Value, 2) == a.Value).WithErrorCode("DEAL_AMOUNT_INVALID")
            .When(x => x.Amount is not null);
        RuleFor(x => x.DurationValue).InclusiveBetween(1, 365).WithErrorCode("INVALID_VALUE").When(x => x.DurationValue is not null);
        RuleFor(x => x.DurationUnit)
            .Must(u => u is null || u.Equals("hour", StringComparison.OrdinalIgnoreCase) || u.Equals("day", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.Description!).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.Description is not null);
    }
}

public sealed class ProposeDealEditHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ProposeDealEditCommand, Result<DealEditDto>>
{
    public async Task<Result<DealEditDto>> Handle(ProposeDealEditCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;
        if (deal.Status != DealStatus.PendingAcceptance) return DealErrors.InvalidState;

        if (await db.DealEditRequests.AnyAsync(e => e.DealId == deal.Id && e.Status == EditStatus.Pending, ct))
            return DealErrors.EditAlreadyPending;

        var newUnit = r.DurationUnit is null ? (DurationUnit?)null
            : r.DurationUnit.Trim().Equals("hour", StringComparison.OrdinalIgnoreCase) ? DurationUnit.Hour : DurationUnit.Day;

        // A proposal that changes nothing is not a proposal.
        var changesSomething =
            (r.Amount is not null && r.Amount.Value != deal.Amount) ||
            (r.DurationValue is not null && r.DurationValue.Value != deal.DurationValue) ||
            (newUnit is not null && newUnit.Value != deal.DurationUnit) ||
            (r.Description is not null && r.Description.Trim() != deal.Description);
        if (!changesSomething) return Error.Validation("INVALID_VALUE", "amount");

        var edit = new DealEditRequest
        {
            DealId = deal.Id,
            ProposedById = userId,
            PrevAmount = deal.Amount,
            PrevDurationValue = deal.DurationValue,
            PrevDurationUnit = deal.DurationUnit.ToString(),
            PrevDescription = deal.Description,
            NewAmount = r.Amount,
            NewDurationValue = r.DurationValue,
            NewDurationUnit = newUnit?.ToString(),
            NewDescription = r.Description?.Trim(),
            Status = EditStatus.Pending
        };
        db.DealEditRequests.Add(edit);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return DealMapper.MapEdit(edit, deal);
    }
}

// ======================================================================= Edit: respond
public sealed record RespondDealEditCommand(long DealId, long EditId, bool Approve) : IRequest<Result<DealDetailDto>>;

public sealed class RespondDealEditHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<RespondDealEditCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(RespondDealEditCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        var edit = await db.DealEditRequests.FirstOrDefaultAsync(e => e.Id == r.EditId && e.DealId == deal.Id, ct);
        if (edit is null || edit.Status != EditStatus.Pending) return DealErrors.EditNotFound;
        if (edit.ProposedById == userId) return DealErrors.NotAllowed;

        var now = clock.UtcNow;
        if (r.Approve)
        {
            if (deal.Status != DealStatus.PendingAcceptance) return DealErrors.InvalidState;
            if (now > deal.RespondDeadlineAt) return DealErrors.ResponseExpired;

            var unit = edit.NewDurationUnit is null ? (DurationUnit?)null
                : edit.NewDurationUnit.Equals("hour", StringComparison.OrdinalIgnoreCase) ? DurationUnit.Hour : DurationUnit.Day;
            var applied = deal.ApplyEdit(edit.NewAmount, edit.NewDurationValue, unit, edit.NewDescription);
            if (applied.IsFailure) return applied.Error;

            edit.Status = EditStatus.Approved;
        }
        else
        {
            edit.Status = EditStatus.Rejected;
        }
        edit.RespondedAt = now;
        edit.RespondedById = userId;

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, now, ct);
    }
}

// ======================================================================= Edit: cancel by proposer
public sealed record CancelDealEditCommand(long DealId, long EditId) : IRequest<Result<DealDetailDto>>;

public sealed class CancelDealEditHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<CancelDealEditCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(CancelDealEditCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        var edit = await db.DealEditRequests.FirstOrDefaultAsync(e => e.Id == r.EditId && e.DealId == deal.Id, ct);
        if (edit is null || edit.Status != EditStatus.Pending) return DealErrors.EditNotFound;
        if (edit.ProposedById != userId) return DealErrors.NotAllowed;

        edit.Status = EditStatus.Cancelled;
        edit.RespondedAt = clock.UtcNow;
        edit.RespondedById = userId;

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, clock.UtcNow, ct);
    }
}
