using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Deals;
using Etamid.Domain.Money;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.CompanyDeals;

// ---------- Inputs ----------

public sealed record CompanyInstallmentInput(string? Label, decimal Amount, DateOnly DueDate);

// ---------- DTOs ----------

public sealed record CompanyDealSummaryDto(
    long Id, long Number, string Title, string CounterpartyName, string CounterpartyCode,
    decimal TotalAmount, string Currency, string Status, string Role,
    DateOnly StartDate, DateTime CreatedAt, IReadOnlyList<string> AvailableActions);

public sealed record CompanyInstallmentDto(
    long Id, byte Order, string? Label, decimal Amount, DateOnly DueDate, string Status,
    DateTime? ApprovalRequestedAt, DateTime? ApprovalDeadlineAt, DateTime? ApprovedAt, DateTime? PaidAt,
    bool IsFunded, decimal? PenaltyAmount, string? PenaltyStatus, string? RejectionReason,
    IReadOnlyList<string> AvailableActions);

public sealed record CompanyPenaltyDto(
    long Id, long InstallmentId, decimal PenaltyPercent, decimal BaseAmount, decimal PenaltyAmount,
    int DaysLate, string Status, string? WaiverReason, DateTime AppliedAt);

public sealed record CompanyPenaltyAdminDto(
    long Id, long CompanyDealId, string DealTitle, long InstallmentId, string? InstallmentLabel,
    decimal PenaltyPercent, decimal BaseAmount, decimal PenaltyAmount, int DaysLate,
    string Status, string? WaiverReason, DateTime AppliedAt);

public sealed record CompanyDealDetailDto(
    long Id, long Number, string Title, string Details, decimal TotalAmount, string Currency, string Status,
    string Role, string CreatorName, string CounterpartyName, string CounterpartyCode,
    DateOnly StartDate, bool HasContract, string? ContractFileUrl,
    int? ApprovalSendDaysBefore, int? ApprovalResponseDays, string? ApprovalNotes,
    decimal? PenaltyPercent, int PenaltyGraceDays, string? PenaltyClauses, byte WizardStep,
    long? RejectReasonId, string? RejectNote,
    DateTime? SubmittedAt, DateTime? AcceptedAt, DateTime? CompletedAt, DateTime CreatedAt,
    IReadOnlyList<CompanyInstallmentDto> Installments, IReadOnlyList<CompanyPenaltyDto> Penalties,
    IReadOnlyList<string> AvailableActions);

public sealed record InstallmentFundSessionDto(
    Guid PaymentId, long CompanyDealId, long InstallmentId, string Status, decimal Amount, string Currency,
    string Provider, string ProviderPaymentId, string RedirectUrl);

// ---------- Mapper ----------

/// <summary>
/// Shapes company deals for the API. Roles mirror the deal cycle's naming:
/// the creator is the payee side ("seller") and the counterparty is the payer
/// side ("buyer") in status-history ActorType terms; the API itself reports
/// the clearer "creator" / "counterparty" roles.
/// </summary>
public static class CompanyDealMapper
{
    public static string RoleOf(CompanyDeal deal, long viewerId)
        => viewerId == deal.CreatorUserId ? "creator" : "counterparty";

    public static async Task<CompanyDealDetailDto> DetailAsync(
        IApplicationDbContext db, CompanyDeal deal, long viewerId, CancellationToken ct, IFileUrlResolver? files = null)
    {
        var creator = await db.Users.AsNoTracking()
            .Where(u => u.Id == deal.CreatorUserId).Select(u => new { u.Name, u.Code }).FirstOrDefaultAsync(ct);
        var counterparty = deal.CounterpartyUserId is { } cpId
            ? await db.Users.AsNoTracking()
                .Where(u => u.Id == cpId).Select(u => new { u.Name, u.Code }).FirstOrDefaultAsync(ct)
            : null;

        var installments = await db.PaymentInstallments.AsNoTracking()
            .Where(i => i.CompanyDealId == deal.Id).OrderBy(i => i.Order).ToListAsync(ct);
        var installmentIds = installments.Select(i => i.Id).ToList();

        var fundedIds = installmentIds.Count == 0 ? new HashSet<long>()
            : (await db.Payments.AsNoTracking()
                .Where(p => p.InstallmentId != null && installmentIds.Contains(p.InstallmentId.Value)
                            && p.Status == PaymentStatus.Succeeded)
                .Select(p => p.InstallmentId!.Value).ToListAsync(ct)).ToHashSet();

        var penalties = await db.PenaltyCharges.AsNoTracking()
            .Where(p => p.CompanyDealId == deal.Id).OrderBy(p => p.AppliedAt).ToListAsync(ct);
        var penaltyByInstallment = penalties.ToDictionary(p => p.InstallmentId);

        // Grouped in memory: a per-group "latest row" projection does not
        // translate to SQL, so the rejected approvals are loaded first.
        var rejectionRows = installmentIds.Count == 0 ? []
            : await db.InstallmentApprovals.AsNoTracking()
                .Where(a => installmentIds.Contains(a.InstallmentId) && a.Decision == ApprovalDecision.Rejected)
                .OrderByDescending(a => a.DecidedAt).ToListAsync(ct);
        var rejections = rejectionRows.GroupBy(a => a.InstallmentId)
            .ToDictionary(g => g.Key, g => g.First().Reason);

        string? contractUrl = null;
        if (deal.ContractFileId is { } fileId && files is not null)
        {
            var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId, ct);
            contractUrl = files.Resolve(file);
        }

        var installmentDtos = installments.Select(i =>
        {
            penaltyByInstallment.TryGetValue(i.Id, out var penalty);
            rejections.TryGetValue(i.Id, out var rejectionReason);
            return new CompanyInstallmentDto(
                i.Id, i.Order, i.Label, i.Amount, i.DueDate, ApiNames.Of(i.Status),
                i.ApprovalRequestedAt, i.ApprovalDeadlineAt, i.ApprovedAt, i.PaidAt,
                fundedIds.Contains(i.Id),
                penalty?.PenaltyAmount, penalty is null ? null : ApiNames.Of(penalty.Status),
                i.Status == InstallmentStatus.Rejected ? rejectionReason : null,
                InstallmentActions(i, deal, viewerId));
        }).ToList();

        return new CompanyDealDetailDto(
            deal.Id, deal.Number, deal.Title, deal.Details, deal.TotalAmount, deal.Currency,
            ApiNames.Of(deal.Status), RoleOf(deal, viewerId),
            creator?.Name ?? string.Empty,
            counterparty?.Name ?? deal.CounterpartyName,
            counterparty?.Code ?? string.Empty,
            deal.StartDate, deal.ContractFileId is not null, contractUrl,
            deal.ApprovalSendDaysBefore, deal.ApprovalResponseDays, deal.ApprovalNotes,
            deal.PenaltyPercent, deal.PenaltyGraceDays, deal.PenaltyClauses, deal.WizardStep,
            deal.RejectReasonId, deal.RejectNote,
            deal.SubmittedAt, deal.AcceptedAt, deal.CompletedAt, deal.CreatedAt,
            installmentDtos,
            penalties.Select(p => new CompanyPenaltyDto(
                p.Id, p.InstallmentId, p.PenaltyPercent, p.BaseAmount, p.PenaltyAmount,
                p.DaysLate, ApiNames.Of(p.Status), p.WaiverReason, p.AppliedAt)).ToList(),
            DealActions(deal, viewerId, installmentDtos));
    }

    public static CompanyDealSummaryDto ToSummary(
        CompanyDeal deal, string counterpartyName, string counterpartyCode, long viewerId,
        IReadOnlyList<string> availableActions)
        => new(deal.Id, deal.Number, deal.Title, counterpartyName, counterpartyCode,
            deal.TotalAmount, deal.Currency, ApiNames.Of(deal.Status), RoleOf(deal, viewerId),
            deal.StartDate, deal.CreatedAt, availableActions);

    /// <summary>Deal-level actions for the viewer (installment actions live on each installment).</summary>
    public static IReadOnlyList<string> DealActions(CompanyDeal deal, long viewerId, IReadOnlyList<CompanyInstallmentDto> installments)
    {
        var actions = new List<string>();
        var isCreator = viewerId == deal.CreatorUserId;
        var isCounterparty = deal.CounterpartyUserId == viewerId;
        switch (deal.Status)
        {
            case CompanyDealStatus.Draft when isCreator:
                actions.Add("edit");
                actions.Add("submit");
                actions.Add("cancel");
                break;
            case CompanyDealStatus.Pending when isCounterparty:
                actions.Add("accept");
                actions.Add("reject");
                break;
            case CompanyDealStatus.Pending when isCreator:
                actions.Add("cancel");
                break;
            case CompanyDealStatus.Active when isCreator:
                if (installments.Any(i => i.Status is "scheduled" or "rejected"))
                    actions.Add("request_approval");
                break;
            case CompanyDealStatus.Active when isCounterparty:
                if (installments.Any(i => i.Status == "awaiting_approval"))
                {
                    actions.Add("approve");
                    actions.Add("reject");
                }
                if (installments.Any(i => i.Status != "paid" && !i.IsFunded))
                    actions.Add("fund");
                break;
        }
        return actions;
    }

    /// <summary>Per-installment actions for the viewer.</summary>
    public static IReadOnlyList<string> InstallmentActions(PaymentInstallment i, CompanyDeal deal, long viewerId)
    {
        var actions = new List<string>();
        if (deal.Status != CompanyDealStatus.Active) return actions;
        var isCreator = viewerId == deal.CreatorUserId;
        var isCounterparty = deal.CounterpartyUserId == viewerId;
        if (isCreator && i.Status is InstallmentStatus.Scheduled or InstallmentStatus.Rejected)
            actions.Add("request_approval");
        if (isCounterparty && i.Status == InstallmentStatus.AwaitingApproval)
        {
            actions.Add("approve");
            actions.Add("reject");
        }
        return actions;
    }

    public static CompanyDealStatusHistory History(
        CompanyDeal deal, CompanyDealStatus? from, CompanyDealStatus to, long actorId, ActorType actorType, string? note)
        => new()
        {
            CompanyDealId = deal.Id,
            FromStatus = from is null ? null : ApiNames.Of(from.Value),
            ToStatus = ApiNames.Of(to),
            ActorUserId = actorId,
            ActorType = actorType,
            Note = note
        };
}
