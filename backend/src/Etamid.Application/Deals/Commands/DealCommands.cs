using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Commands;

// ======================================================================= Create
public sealed record CreateDealCommand(
    string Title, string SellerPhoneOrCode, string Description, decimal Amount,
    int DurationValue, string DurationUnit, bool NeedShipping,
    long? ShippingCompanyId, long? ShippingAddressId, bool AcceptTerms) : IRequest<Result<DealDetailDto>>;

public sealed class CreateDealValidator : AbstractValidator<CreateDealCommand>
{
    public CreateDealValidator()
    {
        RuleFor(x => x.Title).MustBeRequired(150);
        RuleFor(x => x.SellerPhoneOrCode).MustBeRequired(50);
        RuleFor(x => x.Description).MustBeRequired(200);
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithErrorCode("DEAL_AMOUNT_INVALID")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("DEAL_AMOUNT_INVALID")
            .Must(a => decimal.Round(a, 2) == a).WithErrorCode("DEAL_AMOUNT_INVALID");
        RuleFor(x => x.DurationValue).InclusiveBetween(1, 365).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.DurationUnit)
            .NotEmpty().WithErrorCode("REQUIRED")
            .Must(u => u.Equals("hour", StringComparison.OrdinalIgnoreCase) || u.Equals("day", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.ShippingCompanyId).NotNull().WithErrorCode("REQUIRED").When(x => x.NeedShipping);
        RuleFor(x => x.AcceptTerms).Equal(true).WithErrorCode("TERMS_NOT_ACCEPTED");
    }
}

public sealed class CreateDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<CreateDealCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(CreateDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } buyerId) return CommonErrors.Unauthorized;

        var seller = await UserLookup.FindByPhoneOrCodeAsync(db, r.SellerPhoneOrCode, ct);
        if (seller is null) return DealErrors.SellerNotFound;
        if (seller.Id == buyerId) return DealErrors.SameParties;

        if (r.NeedShipping)
        {
            var companyId = r.ShippingCompanyId!.Value;
            if (!await db.ShippingCompanies.AnyAsync(c => c.Id == companyId && c.IsActive, ct))
                return Error.Validation("INVALID_VALUE", "shippingCompanyId");

            if (r.ShippingAddressId is { } addressId &&
                !await db.Addresses.AnyAsync(a => a.Id == addressId && a.UserId == buyerId, ct))
                return Error.Validation("INVALID_VALUE", "shippingAddressId");
        }

        var settings = await DealSettings.LoadAsync(db, ct);
        var unit = r.DurationUnit.Trim().Equals("hour", StringComparison.OrdinalIgnoreCase) ? DurationUnit.Hour : DurationUnit.Day;
        var now = clock.UtcNow;

        var created = Deal.Create(
            buyerId, seller.Id, r.Title, r.Description, r.Amount, r.DurationValue, unit,
            r.NeedShipping, r.ShippingCompanyId, r.ShippingAddressId, imageFileId: null,
            settings.Currency, now, settings.RespondWindow);
        if (created.IsFailure) return created.Error;

        db.Deals.Add(created.Value);
        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, created.Value, buyerId, now, ct);
    }
}

// ======================================================================= Accept
public sealed record AcceptDealCommand(long DealId, bool AcceptTerms) : IRequest<Result<DealDetailDto>>;

public sealed class AcceptDealValidator : AbstractValidator<AcceptDealCommand>
{
    public AcceptDealValidator() => RuleFor(x => x.AcceptTerms).Equal(true).WithErrorCode("TERMS_NOT_ACCEPTED");
}

public sealed class AcceptDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<AcceptDealCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(AcceptDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        // Accepting the original terms must not silently bypass an open edit proposal.
        if (await db.DealEditRequests.AnyAsync(e => e.DealId == deal.Id && e.Status == EditStatus.Pending, ct))
            return DealErrors.InvalidState;

        var settings = await DealSettings.LoadAsync(db, ct);
        var now = clock.UtcNow;

        // Accept freezes price, duration and terms into the digital contract (snapshot + hash).
        var accepted = deal.Accept(userId, settings.PlatformFee, now, settings.FundingWindow);
        if (accepted.IsFailure) return accepted.Error;

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, now, ct);
    }
}

// ======================================================================= Reject
public sealed record RejectDealCommand(long DealId, long ReasonId, string? Note) : IRequest<Result<DealDetailDto>>;

public sealed class RejectDealValidator : AbstractValidator<RejectDealCommand>
{
    public RejectDealValidator()
    {
        RuleFor(x => x.ReasonId).GreaterThan(0).WithErrorCode("REQUIRED");
        RuleFor(x => x.Note!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.Note is not null);
    }
}

public sealed class RejectDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<RejectDealCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(RejectDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        if (!await db.Lookups.AnyAsync(l => l.Id == r.ReasonId && l.Type == "reject_reason" && l.IsActive, ct))
            return Error.Validation("INVALID_VALUE", "reasonId");

        var now = clock.UtcNow;
        var rejected = deal.Reject(userId, r.ReasonId, r.Note, now);
        if (rejected.IsFailure) return rejected.Error;

        // A rejected deal has no open proposals left.
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

// ======================================================================= Deliver
public sealed record DeliverDealCommand(long DealId) : IRequest<Result<DealDetailDto>>;

public sealed class DeliverDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<DeliverDealCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(DeliverDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        var hold = await db.EscrowHolds.AsNoTracking().FirstOrDefaultAsync(h => h.DealId == deal.Id, ct);
        var now = clock.UtcNow;

        // Delivery is only possible against money that is really held in escrow.
        var delivered = deal.Deliver(userId, now);
        if (delivered.IsFailure) return delivered.Error;
        if (hold is null || hold.Status != EscrowStatus.Held) return DealErrors.EscrowNotHeld;

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, now, ct);
    }
}

// ======================================================================= Dispute
public sealed record OpenDisputeCommand(long DealId, long ReasonId, string Details) : IRequest<Result<DisputeDto>>;

public sealed class OpenDisputeValidator : AbstractValidator<OpenDisputeCommand>
{
    public OpenDisputeValidator()
    {
        RuleFor(x => x.ReasonId).GreaterThan(0).WithErrorCode("REQUIRED");
        RuleFor(x => x.Details).MustBeRequired(200);
    }
}

public sealed class OpenDisputeHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<OpenDisputeCommand, Result<DisputeDto>>
{
    public async Task<Result<DisputeDto>> Handle(OpenDisputeCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        if (!await db.Lookups.AnyAsync(l => l.Id == r.ReasonId && l.Type == "dispute_reason" && l.IsActive, ct))
            return Error.Validation("INVALID_VALUE", "reasonId");

        var hold = await db.EscrowHolds.FirstOrDefaultAsync(h => h.DealId == deal.Id, ct);
        var now = clock.UtcNow;

        // From here on nothing releases this money automatically: the deal is Disputed and the hold is frozen.
        var opened = deal.OpenDispute(userId, now);
        if (opened.IsFailure) return opened.Error;
        if (hold is null || hold.Status != EscrowStatus.Held) return DealErrors.EscrowNotHeld;
        hold.Status = EscrowStatus.FrozenDispute;

        var dispute = new Dispute
        {
            Number = $"DSP-{deal.Id:D6}",
            DealId = deal.Id,
            OpenedById = userId,
            AgainstUserId = deal.SellerId,
            ReasonId = r.ReasonId,
            Details = r.Details.Trim(),
            Status = DisputeStatus.UnderReview,
            Resolution = DisputeResolution.None
        };
        db.Disputes.Add(dispute);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return new DisputeDto(dispute.Id, dispute.Number, deal.Id, ApiNames.Of(dispute.Status));
    }
}
