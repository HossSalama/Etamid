using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Deals;
using Etamid.Domain.Files;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.CompanyDeals;

// ======================================================================= Create draft
public sealed record CreateCompanyDealCommand(
    string CounterpartyPhoneOrCode, string Title, string Details, decimal TotalAmount,
    DateOnly StartDate, string? ContractFilePublicId,
    int? ApprovalSendDaysBefore, int? ApprovalResponseDays, string? ApprovalNotes,
    decimal? PenaltyPercent, int? PenaltyGraceDays, string? PenaltyClauses,
    byte WizardStep, IReadOnlyList<CompanyInstallmentInput> Installments)
    : IRequest<Result<CompanyDealDetailDto>>;

public sealed class CreateCompanyDealValidator : AbstractValidator<CreateCompanyDealCommand>
{
    public CreateCompanyDealValidator()
    {
        RuleFor(x => x.CounterpartyPhoneOrCode).MustBeRequired(50);
        RuleFor(x => x.Title).MustBeRequired(150);
        RuleFor(x => x.Details).MustBeRequired(200);
        RuleFor(x => x.TotalAmount)
            .GreaterThan(0m).WithErrorCode("INVALID_VALUE")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("INVALID_VALUE")
            .Must(a => decimal.Round(a, 2) == a).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.StartDate).Must(d => d != default).WithErrorCode("REQUIRED");
        RuleFor(x => x.ContractFilePublicId!)
            .Must(id => string.IsNullOrWhiteSpace(id) || Guid.TryParse(id.Trim(), out _))
            .WithErrorCode("INVALID_VALUE").When(x => x.ContractFilePublicId is not null);
        RuleFor(x => x.ApprovalSendDaysBefore!.Value).InclusiveBetween(0, 365).WithErrorCode("INVALID_VALUE")
            .When(x => x.ApprovalSendDaysBefore is not null);
        RuleFor(x => x.ApprovalResponseDays!.Value).InclusiveBetween(0, 365).WithErrorCode("INVALID_VALUE")
            .When(x => x.ApprovalResponseDays is not null);
        RuleFor(x => x.ApprovalNotes!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.ApprovalNotes is not null);
        RuleFor(x => x.PenaltyPercent!.Value).InclusiveBetween(0m, 100m).WithErrorCode("INVALID_VALUE")
            .When(x => x.PenaltyPercent is not null);
        RuleFor(x => x.PenaltyGraceDays!.Value).InclusiveBetween(0, 365).WithErrorCode("INVALID_VALUE")
            .When(x => x.PenaltyGraceDays is not null);
        RuleFor(x => x.PenaltyClauses!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.PenaltyClauses is not null);
        RuleFor(x => x.Installments).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.Installments).Must(l => l.Count <= 100).WithErrorCode("TOO_LONG");
        RuleFor(x => x.Installments)
            .Must((cmd, list) => list.Sum(i => i.Amount) == cmd.TotalAmount)
            .WithErrorCode("COMPANY_INSTALLMENTS_SUM_MISMATCH");
        RuleForEach(x => x.Installments).ChildRules(item =>
        {
            item.RuleFor(x => x.Label!).MaximumLength(50).WithErrorCode("TOO_LONG").When(x => x.Label is not null);
            item.RuleFor(x => x.Amount)
                .GreaterThan(0m).WithErrorCode("INVALID_VALUE")
                .Must(a => decimal.Round(a, 2) == a).WithErrorCode("INVALID_VALUE");
            item.RuleFor(x => x.DueDate).Must(d => d != default).WithErrorCode("REQUIRED");
        });
    }
}

public sealed class CreateCompanyDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<CreateCompanyDealCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(CreateCompanyDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } creatorId) return CommonErrors.Unauthorized;

        var counterparty = await UserLookup.FindByPhoneOrCodeAsync(db, r.CounterpartyPhoneOrCode, ct);
        if (counterparty is null) return CompanyDealErrors.CounterpartyNotFound;
        if (counterparty.Id == creatorId) return CompanyDealErrors.SameParties;

        var contractFileId = await CompanyDealSupport.ResolveContractFileAsync(db, creatorId, r.ContractFilePublicId, ct);
        if (contractFileId.IsFailure) return contractFileId.Error;

        var deal = new CompanyDeal
        {
            CreatorUserId = creatorId,
            CounterpartyUserId = counterparty.Id,
            CounterpartyName = counterparty.Name,
            CounterpartyPhone = counterparty.Phone,
            Title = r.Title,
            Details = r.Details,
            TotalAmount = r.TotalAmount,
            Currency = "SAR",
            StartDate = r.StartDate,
            ContractFileId = contractFileId.Value,
            ApprovalSendDaysBefore = r.ApprovalSendDaysBefore,
            ApprovalResponseDays = r.ApprovalResponseDays,
            ApprovalNotes = r.ApprovalNotes,
            PenaltyPercent = r.PenaltyPercent,
            PenaltyGraceDays = r.PenaltyGraceDays ?? 30,
            PenaltyClauses = r.PenaltyClauses,
            WizardStep = r.WizardStep,
            Status = CompanyDealStatus.Draft
        };
        db.CompanyDeals.Add(deal);
        await db.SaveChangesAsync(ct);

        // The deal row must exist before its installments (foreign key).
        byte order = 0;
        foreach (var input in r.Installments)
        {
            order++;
            db.PaymentInstallments.Add(new PaymentInstallment
            {
                CompanyDealId = deal.Id,
                Order = order,
                Label = input.Label,
                Amount = input.Amount,
                DueDate = input.DueDate,
                Status = InstallmentStatus.Scheduled
            });
        }
        db.CompanyDealStatusHistory.Add(CompanyDealMapper.History(deal, null, CompanyDealStatus.Draft, creatorId, ActorType.Seller, null));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, creatorId, ct, files);
    }
}

// ======================================================================= Edit draft
public sealed record EditCompanyDealCommand(
    long Id, string Title, string Details, decimal TotalAmount,
    DateOnly StartDate, string? ContractFilePublicId,
    int? ApprovalSendDaysBefore, int? ApprovalResponseDays, string? ApprovalNotes,
    decimal? PenaltyPercent, int? PenaltyGraceDays, string? PenaltyClauses,
    byte WizardStep, IReadOnlyList<CompanyInstallmentInput> Installments)
    : IRequest<Result<CompanyDealDetailDto>>;

public sealed class EditCompanyDealValidator : AbstractValidator<EditCompanyDealCommand>
{
    public EditCompanyDealValidator()
    {
        RuleFor(x => x.Title).MustBeRequired(150);
        RuleFor(x => x.Details).MustBeRequired(200);
        RuleFor(x => x.TotalAmount)
            .GreaterThan(0m).WithErrorCode("INVALID_VALUE")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("INVALID_VALUE")
            .Must(a => decimal.Round(a, 2) == a).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.StartDate).Must(d => d != default).WithErrorCode("REQUIRED");
        RuleFor(x => x.ContractFilePublicId!)
            .Must(id => string.IsNullOrWhiteSpace(id) || Guid.TryParse(id.Trim(), out _))
            .WithErrorCode("INVALID_VALUE").When(x => x.ContractFilePublicId is not null);
        RuleFor(x => x.ApprovalSendDaysBefore!.Value).InclusiveBetween(0, 365).WithErrorCode("INVALID_VALUE")
            .When(x => x.ApprovalSendDaysBefore is not null);
        RuleFor(x => x.ApprovalResponseDays!.Value).InclusiveBetween(0, 365).WithErrorCode("INVALID_VALUE")
            .When(x => x.ApprovalResponseDays is not null);
        RuleFor(x => x.ApprovalNotes!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.ApprovalNotes is not null);
        RuleFor(x => x.PenaltyPercent!.Value).InclusiveBetween(0m, 100m).WithErrorCode("INVALID_VALUE")
            .When(x => x.PenaltyPercent is not null);
        RuleFor(x => x.PenaltyGraceDays!.Value).InclusiveBetween(0, 365).WithErrorCode("INVALID_VALUE")
            .When(x => x.PenaltyGraceDays is not null);
        RuleFor(x => x.PenaltyClauses!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.PenaltyClauses is not null);
        RuleFor(x => x.Installments).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.Installments).Must(l => l.Count <= 100).WithErrorCode("TOO_LONG");
        RuleFor(x => x.Installments)
            .Must((cmd, list) => list.Sum(i => i.Amount) == cmd.TotalAmount)
            .WithErrorCode("COMPANY_INSTALLMENTS_SUM_MISMATCH");
        RuleForEach(x => x.Installments).ChildRules(item =>
        {
            item.RuleFor(x => x.Label!).MaximumLength(50).WithErrorCode("TOO_LONG").When(x => x.Label is not null);
            item.RuleFor(x => x.Amount)
                .GreaterThan(0m).WithErrorCode("INVALID_VALUE")
                .Must(a => decimal.Round(a, 2) == a).WithErrorCode("INVALID_VALUE");
            item.RuleFor(x => x.DueDate).Must(d => d != default).WithErrorCode("REQUIRED");
        });
    }
}

public sealed class EditCompanyDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<EditCompanyDealCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(EditCompanyDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } creatorId) return CommonErrors.Unauthorized;

        var deal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == r.Id, ct);
        if (deal is null || (deal.CreatorUserId != creatorId && deal.CounterpartyUserId != creatorId)) return CompanyDealErrors.NotFound;
        if (deal.CreatorUserId != creatorId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Draft) return CompanyDealErrors.InvalidState;

        // A new contract file replaces the old one; null keeps it. Draft
        // installments have no money attached, so replacing the schedule is safe.
        if (!string.IsNullOrWhiteSpace(r.ContractFilePublicId))
        {
            var contractFileId = await CompanyDealSupport.ResolveContractFileAsync(db, creatorId, r.ContractFilePublicId, ct);
            if (contractFileId.IsFailure) return contractFileId.Error;
            deal.ContractFileId = contractFileId.Value;
        }

        deal.Title = r.Title;
        deal.Details = r.Details;
        deal.TotalAmount = r.TotalAmount;
        deal.StartDate = r.StartDate;
        deal.ApprovalSendDaysBefore = r.ApprovalSendDaysBefore;
        deal.ApprovalResponseDays = r.ApprovalResponseDays;
        deal.ApprovalNotes = r.ApprovalNotes;
        deal.PenaltyPercent = r.PenaltyPercent;
        deal.PenaltyGraceDays = r.PenaltyGraceDays ?? 30;
        deal.PenaltyClauses = r.PenaltyClauses;
        deal.WizardStep = r.WizardStep;

        var old = await db.PaymentInstallments.Where(i => i.CompanyDealId == deal.Id).ToListAsync(ct);
        db.PaymentInstallments.RemoveRange(old);
        byte order = 0;
        foreach (var input in r.Installments)
        {
            order++;
            db.PaymentInstallments.Add(new PaymentInstallment
            {
                CompanyDealId = deal.Id,
                Order = order,
                Label = input.Label,
                Amount = input.Amount,
                DueDate = input.DueDate,
                Status = InstallmentStatus.Scheduled
            });
        }

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, creatorId, ct, files);
    }
}

// ======================================================================= Submit / accept / reject / cancel
public sealed record SubmitCompanyDealCommand(long Id) : IRequest<Result<CompanyDealDetailDto>>;

public sealed class SubmitCompanyDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files)
    : IRequestHandler<SubmitCompanyDealCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(SubmitCompanyDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } creatorId) return CommonErrors.Unauthorized;
        var deal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == r.Id, ct);
        if (deal is null || (deal.CreatorUserId != creatorId && deal.CounterpartyUserId != creatorId)) return CompanyDealErrors.NotFound;
        if (deal.CreatorUserId != creatorId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Draft) return CompanyDealErrors.InvalidState;

        deal.Status = CompanyDealStatus.Pending;
        deal.SubmittedAt = clock.UtcNow;
        db.CompanyDealStatusHistory.Add(CompanyDealMapper.History(deal, CompanyDealStatus.Draft, CompanyDealStatus.Pending, creatorId, ActorType.Seller, null));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, creatorId, ct, files);
    }
}

public sealed record AcceptCompanyDealCommand(long Id, bool AcceptTerms) : IRequest<Result<CompanyDealDetailDto>>;

public sealed class AcceptCompanyDealValidator : AbstractValidator<AcceptCompanyDealCommand>
{
    public AcceptCompanyDealValidator() => RuleFor(x => x.AcceptTerms).Equal(true).WithErrorCode("TERMS_NOT_ACCEPTED");
}

public sealed class AcceptCompanyDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files)
    : IRequestHandler<AcceptCompanyDealCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(AcceptCompanyDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var deal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == r.Id, ct);
        if (deal is null || (deal.CreatorUserId != userId && deal.CounterpartyUserId != userId)) return CompanyDealErrors.NotFound;
        if (deal.CounterpartyUserId != userId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Pending) return CompanyDealErrors.InvalidState;

        deal.Status = CompanyDealStatus.Active;
        deal.AcceptedAt = clock.UtcNow;
        db.CompanyDealStatusHistory.Add(CompanyDealMapper.History(deal, CompanyDealStatus.Pending, CompanyDealStatus.Active, userId, ActorType.Buyer, null));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, userId, ct, files);
    }
}

public sealed record RejectCompanyDealCommand(long Id, long ReasonId, string? Note) : IRequest<Result<CompanyDealDetailDto>>;

public sealed class RejectCompanyDealValidator : AbstractValidator<RejectCompanyDealCommand>
{
    public RejectCompanyDealValidator()
    {
        RuleFor(x => x.ReasonId).GreaterThan(0).WithErrorCode("REQUIRED");
        RuleFor(x => x.Note!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.Note is not null);
    }
}

public sealed class RejectCompanyDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files)
    : IRequestHandler<RejectCompanyDealCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(RejectCompanyDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var deal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == r.Id, ct);
        if (deal is null || (deal.CreatorUserId != userId && deal.CounterpartyUserId != userId)) return CompanyDealErrors.NotFound;
        if (deal.CounterpartyUserId != userId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Pending) return CompanyDealErrors.InvalidState;

        if (!await db.Lookups.AnyAsync(l => l.Id == r.ReasonId && l.Type == "company_reject_reason" && l.IsActive, ct))
            return Error.Validation("INVALID_VALUE", "reasonId");

        deal.Status = CompanyDealStatus.Rejected;
        deal.RejectReasonId = r.ReasonId;
        deal.RejectNote = r.Note;
        db.CompanyDealStatusHistory.Add(CompanyDealMapper.History(deal, CompanyDealStatus.Pending, CompanyDealStatus.Rejected, userId, ActorType.Buyer, r.Note));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, userId, ct, files);
    }
}

public sealed record CancelCompanyDealCommand(long Id) : IRequest<Result<CompanyDealDetailDto>>;

public sealed class CancelCompanyDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files)
    : IRequestHandler<CancelCompanyDealCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(CancelCompanyDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } creatorId) return CommonErrors.Unauthorized;
        var deal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == r.Id, ct);
        if (deal is null || (deal.CreatorUserId != creatorId && deal.CounterpartyUserId != creatorId)) return CompanyDealErrors.NotFound;
        if (deal.CreatorUserId != creatorId) return CompanyDealErrors.NotAllowed;
        if (deal.Status is not (CompanyDealStatus.Draft or CompanyDealStatus.Pending)) return CompanyDealErrors.InvalidState;

        var from = deal.Status;
        deal.Status = CompanyDealStatus.Cancelled;
        db.CompanyDealStatusHistory.Add(CompanyDealMapper.History(deal, from, CompanyDealStatus.Cancelled, creatorId, ActorType.Seller, null));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, creatorId, ct, files);
    }
}

// ======================================================================= List / detail
public sealed record GetCompanyDealsQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<CompanyDealSummaryDto>>>;

public sealed class GetCompanyDealsHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetCompanyDealsQuery, Result<PagedResult<CompanyDealSummaryDto>>>
{
    public async Task<Result<PagedResult<CompanyDealSummaryDto>>> Handle(GetCompanyDealsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        CompanyDealStatus? status = null;
        if (!string.IsNullOrWhiteSpace(r.Status))
        {
            if (!Enum.TryParse<CompanyDealStatus>(r.Status, ignoreCase: true, out var parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);
        var query = db.CompanyDeals.AsNoTracking()
            .Where(d => d.CreatorUserId == userId || d.CounterpartyUserId == userId);
        if (status is not null) query = query.Where(d => d.Status == status);
        var total = await query.CountAsync(ct);
        var deals = await query
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var dealIds = deals.Select(d => d.Id).ToList();
        var installments = dealIds.Count == 0 ? []
            : await db.PaymentInstallments.AsNoTracking()
                .Where(i => dealIds.Contains(i.CompanyDealId)).ToListAsync(ct);
        var installmentIds = installments.Select(i => i.Id).ToList();
        var fundedIds = installmentIds.Count == 0 ? new HashSet<long>()
            : (await db.Payments.AsNoTracking()
                .Where(p => p.InstallmentId != null && installmentIds.Contains(p.InstallmentId.Value)
                            && p.Status == PaymentStatus.Succeeded)
                .Select(p => p.InstallmentId!.Value).ToListAsync(ct)).ToHashSet();

        var otherIds = deals.Select(d => d.CreatorUserId == userId ? d.CounterpartyUserId!.Value : d.CreatorUserId).Distinct().ToList();
        var others = await db.Users.AsNoTracking()
            .Where(u => otherIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        var items = deals.Select(d =>
        {
            var otherId = d.CreatorUserId == userId ? d.CounterpartyUserId!.Value : d.CreatorUserId;
            others.TryGetValue(otherId, out var other);
            var mine = installments.Where(i => i.CompanyDealId == d.Id).Select(i => new CompanyInstallmentDto(
                i.Id, i.Order, i.Label, i.Amount, i.DueDate, ApiNames.Of(i.Status),
                i.ApprovalRequestedAt, i.ApprovalDeadlineAt, i.ApprovedAt, i.PaidAt,
                fundedIds.Contains(i.Id), null, null, null, [])).ToList();
            return CompanyDealMapper.ToSummary(d, other?.Name ?? d.CounterpartyName, other?.Code ?? string.Empty, userId,
                CompanyDealMapper.DealActions(d, userId, mine));
        }).ToList();
        return new PagedResult<CompanyDealSummaryDto>(items, page, pageSize, total);
    }
}

public sealed record GetCompanyDealQuery(long Id) : IRequest<Result<CompanyDealDetailDto>>;

public sealed class GetCompanyDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<GetCompanyDealQuery, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(GetCompanyDealQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var deal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == r.Id, ct);
        if (deal is null || (deal.CreatorUserId != userId && deal.CounterpartyUserId != userId))
            return CompanyDealErrors.NotFound;
        return await CompanyDealMapper.DetailAsync(db, deal, userId, ct, files);
    }
}

// ======================================================================= Shared
public static class CompanyDealSupport
{
    /// <summary>
    /// A contract file is optional, but when supplied it must be a file the
    /// creator uploaded for this purpose — never another user's file, a
    /// non-contract, or a deleted/quarantined one. The same generic error
    /// covers all cases so file ids cannot be probed (same as deal images).
    /// </summary>
    public static async Task<Result<long?>> ResolveContractFileAsync(
        IApplicationDbContext db, long creatorId, string? publicId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(publicId) || !Guid.TryParse(publicId.Trim(), out var guid))
            return Result.Success<long?>(null);

        var file = await db.Files.FirstOrDefaultAsync(f => f.PublicId == guid, ct);
        if (file is null || file.OwnerUserId != creatorId || file.Purpose != FilePurpose.Contract
            || file.Status != FileStatus.Uploaded)
            return Error.Validation("INVALID_VALUE", "contractFilePublicId");

        file.Status = FileStatus.Attached;
        return Result.Success<long?>(file.Id);
    }

    /// <summary>Loads a deal the caller participates in, or null (callers map null to NotFound).</summary>
    public static async Task<CompanyDeal?> FindForParticipantAsync(
        IApplicationDbContext db, long dealId, long userId, CancellationToken ct)
    {
        var deal = await db.CompanyDeals.FirstOrDefaultAsync(d => d.Id == dealId, ct);
        return deal is null || (deal.CreatorUserId != userId && deal.CounterpartyUserId != userId) ? null : deal;
    }

    public static async Task<PaymentInstallment?> FindInstallmentAsync(
        IApplicationDbContext db, long dealId, long installmentId, CancellationToken ct)
        => await db.PaymentInstallments.FirstOrDefaultAsync(
            i => i.Id == installmentId && i.CompanyDealId == dealId, ct);

    public static async Task<bool> HasSucceededPaymentAsync(
        IApplicationDbContext db, long installmentId, CancellationToken ct)
        => await db.Payments.AnyAsync(
            p => p.InstallmentId == installmentId && p.Status == PaymentStatus.Succeeded, ct);
}
