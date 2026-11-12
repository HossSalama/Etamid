using Etamid.Application.AdminAuth;
using Etamid.Domain.Admin;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Application.Notifications;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Deals;
using Etamid.Domain.Money;
using Etamid.Domain.Notifications;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.CompanyDeals;

// ---------- Scheduled work (system, never exposed over HTTP except via the worker) ----------

public sealed record CompanyMaintenanceDto(int OverdueCount, int PenaltiesAppliedCount);

public sealed record RunCompanyInstallmentMaintenanceCommand : IRequest<Result<CompanyMaintenanceDto>>;

public sealed class RunCompanyInstallmentMaintenanceHandler(IApplicationDbContext db, IClock clock, NotificationService notifications)
    : IRequestHandler<RunCompanyInstallmentMaintenanceCommand, Result<CompanyMaintenanceDto>>
{
    public async Task<Result<CompanyMaintenanceDto>> Handle(RunCompanyInstallmentMaintenanceCommand request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var now = clock.UtcNow;

        // Only Active deals, only installments nobody has paid for yet: a
        // funded installment is not late even if its approval is still open —
        // the payer paid on time, so no penalty can attach to it.
        var candidates = await db.PaymentInstallments
            .Where(i => i.Status == InstallmentStatus.Scheduled || i.Status == InstallmentStatus.Rejected
                || i.Status == InstallmentStatus.AwaitingApproval || i.Status == InstallmentStatus.Approved || i.Status == InstallmentStatus.Overdue)
            .Join(db.CompanyDeals.Where(d => d.Status == CompanyDealStatus.Active),
                i => i.CompanyDealId, d => d.Id, (i, d) => new { Installment = i, Deal = d })
            .Where(x => x.Installment.DueDate < today)
            .ToListAsync(ct);

        // Id lists are materialized first: EF cannot translate a Select over
        // an in-memory list inside a query, but it can translate Contains
        // over a plain list.
        var candidateIds = candidates.Select(x => x.Installment.Id).ToList();
        var fundedIds = candidateIds.Count == 0 ? new HashSet<long>()
            : (await db.Payments.AsNoTracking()
                .Where(p => p.InstallmentId != null && candidateIds.Contains(p.InstallmentId.Value)
                    && p.Status == PaymentStatus.Succeeded)
                .Select(p => p.InstallmentId!.Value).ToListAsync(ct)).ToHashSet();

        var existingPenalties = candidateIds.Count == 0 ? new HashSet<long>()
            : (await db.PenaltyCharges.AsNoTracking()
                .Where(p => candidateIds.Contains(p.InstallmentId))
                .Select(p => p.InstallmentId).ToListAsync(ct)).ToHashSet();

        var overdue = 0;
        var penalties = 0;
        foreach (var candidate in candidates)
        {
            var installment = candidate.Installment;
            var deal = candidate.Deal;
            if (fundedIds.Contains(installment.Id)) continue;

            if (installment.Status != InstallmentStatus.Overdue
                && installment.Status != InstallmentStatus.PenaltyApplied)
            {
                installment.Status = InstallmentStatus.Overdue;
                overdue++;

                // The payer is late: the counterparty (funder) is the one who must act.
                if (deal.CounterpartyUserId is { } payerId)
                    notifications.Create(payerId, NotificationType.PaymentDue,
                        "NOTIF_PAYMENT_DUE_TITLE", "NOTIF_PAYMENT_DUE_BODY",
                        installment.Label ?? deal.Title, companyDealId: deal.Id, installmentId: installment.Id);
            }

            // One penalty per installment (unique index): past due date +
            // grace days, and only when the deal carries a penalty percent.
            // The charge is a recorded debt; it is settled only when the
            // payer funds amount + penalty (see CompanyEscrowService).
            var percent = deal.PenaltyPercent;
            if (percent is null or <= 0m) continue;
            if (existingPenalties.Contains(installment.Id)) continue;
            if (installment.DueDate.AddDays(deal.PenaltyGraceDays) >= today) continue;

            var daysLate = today.DayNumber - installment.DueDate.DayNumber;
            var amount = decimal.Round(installment.Amount * percent.Value / 100m, 2);
            if (amount <= 0m) continue;

            db.PenaltyCharges.Add(new PenaltyCharge
            {
                InstallmentId = installment.Id,
                CompanyDealId = deal.Id,
                PenaltyPercent = percent.Value,
                BaseAmount = installment.Amount,
                PenaltyAmount = amount,
                DaysLate = daysLate,
                AppliedAt = now,
                Status = PenaltyStatus.Applied
            });
            installment.Status = InstallmentStatus.PenaltyApplied;
            penalties++;

            // The penalty is owed to the harmed party — the creator (payee) — not the platform.
            notifications.Create(deal.CreatorUserId, NotificationType.Penalty,
                "NOTIF_PENALTY_TITLE", "NOTIF_PENALTY_BODY",
                installment.Label ?? deal.Title, companyDealId: deal.Id, installmentId: installment.Id);
        }

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return new CompanyMaintenanceDto(overdue, penalties);
    }
}

// ---------- Operator: list penalties ----------
// Cycle 9 phase 2: gated by an AdminJwt identity plus a database-checked permission.

public sealed record ListCompanyPenaltiesQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<CompanyPenaltyAdminDto>>>;

public sealed class ListCompanyPenaltiesHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListCompanyPenaltiesQuery, Result<PagedResult<CompanyPenaltyAdminDto>>>
{
    public async Task<Result<PagedResult<CompanyPenaltyAdminDto>>> Handle(ListCompanyPenaltiesQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.PenaltiesView, ct);
        if (guard.IsFailure) return guard.Error;

        PenaltyStatus? status = null;
        if (!string.IsNullOrWhiteSpace(r.Status))
        {
            if (!Enum.TryParse<PenaltyStatus>(r.Status, ignoreCase: true, out var parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);
        var query = db.PenaltyCharges.AsNoTracking();
        if (status is not null) query = query.Where(p => p.Status == status);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.AppliedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var dealIds = items.Select(p => p.CompanyDealId).Distinct().ToList();
        var titles = await db.CompanyDeals.AsNoTracking()
            .Where(d => dealIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Title, ct);
        var installmentIds = items.Select(p => p.InstallmentId).Distinct().ToList();
        var labels = await db.PaymentInstallments.AsNoTracking()
            .Where(i => installmentIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Label, ct);

        var dtos = items.Select(p => new CompanyPenaltyAdminDto(
            p.Id, p.CompanyDealId, titles.GetValueOrDefault(p.CompanyDealId, string.Empty),
            p.InstallmentId, labels.GetValueOrDefault(p.InstallmentId),
            p.PenaltyPercent, p.BaseAmount, p.PenaltyAmount, p.DaysLate,
            ApiNames.Of(p.Status), p.WaiverReason, p.AppliedAt)).ToList();
        return new PagedResult<CompanyPenaltyAdminDto>(dtos, page, pageSize, total);
    }
}

// ---------- Operator: waive penalty ----------

public sealed record WaiveCompanyPenaltyCommand(long PenaltyId, string Reason) : IRequest<Result<CompanyPenaltyDto>>;

public sealed class WaiveCompanyPenaltyValidator : AbstractValidator<WaiveCompanyPenaltyCommand>
{
    public WaiveCompanyPenaltyValidator()
        => RuleFor(x => x.Reason).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(300).WithErrorCode("TOO_LONG");
}

public sealed class WaiveCompanyPenaltyHandler(IApplicationDbContext db, AdminAccessService access, AdminAuditService audit)
    : IRequestHandler<WaiveCompanyPenaltyCommand, Result<CompanyPenaltyDto>>
{
    public async Task<Result<CompanyPenaltyDto>> Handle(WaiveCompanyPenaltyCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.PenaltiesWaive, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var penalty = await db.PenaltyCharges.FirstOrDefaultAsync(p => p.Id == r.PenaltyId, ct);
        if (penalty is null) return CompanyDealErrors.PenaltyNotFound;
        if (penalty.Status != PenaltyStatus.Applied) return CompanyDealErrors.InvalidState;

        var before = penalty.Status;
        penalty.Status = PenaltyStatus.Waived;
        penalty.WaiverReason = r.Reason;
        penalty.WaivedByAdminId = admin.Id;

        // The installment is still owed, just without the penalty: it goes
        // back to Overdue so a later funding covers the plain amount.
        var installment = await db.PaymentInstallments.FirstOrDefaultAsync(i => i.Id == penalty.InstallmentId, ct);
        if (installment is not null && installment.Status == InstallmentStatus.PenaltyApplied)
            installment.Status = InstallmentStatus.Overdue;

        audit.Record(AuditActorType.Admin, admin.Id, "admin.penalty.waive", "PenaltyCharge", penalty.Id,
            new { status = before.ToString() }, new { status = penalty.Status.ToString(), reason = r.Reason });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return new CompanyPenaltyDto(
            penalty.Id, penalty.InstallmentId, penalty.PenaltyPercent, penalty.BaseAmount, penalty.PenaltyAmount,
            penalty.DaysLate, ApiNames.Of(penalty.Status), penalty.WaiverReason, penalty.AppliedAt);
    }
}
