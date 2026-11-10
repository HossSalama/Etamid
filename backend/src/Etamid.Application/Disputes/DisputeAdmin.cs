using Etamid.Application.AdminAuth;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Disputes;

/// <summary>
/// Dispute operators are app users listed in the <c>disputes.operators</c> AppSetting
/// (comma-separated ids, writable only via the database).
/// </summary>
public static class DisputeOperatorPolicy
{
    public const string SettingKey = "disputes.operators";

    public static async Task<bool> IsOperatorAsync(IApplicationDbContext db, long userId, CancellationToken ct)
    {
        // All rows with the key count (the key is the table PK in SQL Server,
        // but the in-memory provider does not enforce it, so never assume one).
        var values = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == SettingKey).Select(s => s.Value).ToListAsync(ct);
        return values.SelectMany(v => v.Split(','))
            .Select(p => long.TryParse(p.Trim(), out var id) ? id : -1).Contains(userId);
    }
}

// ---------- GET /admin/disputes ----------
// Cycle 9 phase 2: gated by an AdminJwt identity plus a database-checked permission. DisputeOperatorPolicy and the
// disputes.operators key are kept (user-facing dispute endpoints still use the policy for operator messages).

public sealed record ListDisputesQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<DisputeSummaryDto>>>;

public sealed class ListDisputesHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListDisputesQuery, Result<PagedResult<DisputeSummaryDto>>>
{
    public async Task<Result<PagedResult<DisputeSummaryDto>>> Handle(ListDisputesQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(Etamid.Domain.Admin.AdminPermissionCodes.DisputesView, ct);
        if (guard.IsFailure) return guard.Error;

        DisputeStatus? status = null;
        if (!string.IsNullOrWhiteSpace(r.Status))
        {
            if (!Enum.TryParse<DisputeStatus>(r.Status, ignoreCase: true, out var parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.Disputes.AsNoTracking();
        if (status is { } s) query = query.Where(d => d.Status == s);

        var total = await query.CountAsync(ct);
        var disputes = await query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = new List<DisputeSummaryDto>();
        foreach (var dispute in disputes)
            items.Add(await DisputeMapper.SummaryAsync(db, dispute, ct));

        return new PagedResult<DisputeSummaryDto>(items, page, pageSize, total);
    }
}

// ---------- POST /admin/disputes/{id}/request-info ----------

public sealed record RequestDisputeInfoCommand(long DisputeId, string? Note, int DeadlineDays)
    : IRequest<Result<DisputeDetailDto>>;

public sealed class RequestDisputeInfoValidator : AbstractValidator<RequestDisputeInfoCommand>
{
    public RequestDisputeInfoValidator()
    {
        RuleFor(x => x.DeadlineDays).InclusiveBetween(1, 90);
        RuleFor(x => x.Note!).MaximumLength(500).WithErrorCode("TOO_LONG").When(x => x.Note is not null);
    }
}

public sealed class RequestDisputeInfoHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IClock clock, IFileUrlResolver files)
    : IRequestHandler<RequestDisputeInfoCommand, Result<DisputeDetailDto>>
{
    public async Task<Result<DisputeDetailDto>> Handle(RequestDisputeInfoCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(Etamid.Domain.Admin.AdminPermissionCodes.DisputesDecide, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var dispute = await db.Disputes.FirstOrDefaultAsync(d => d.Id == r.DisputeId, ct);
        if (dispute is null) return DisputeErrors.NotFound;
        if (!DisputeMapper.IsOpen(dispute.Status)) return DisputeErrors.AlreadyResolved;

        var now = clock.UtcNow;
        var before = dispute.Status;
        dispute.Status = DisputeStatus.AwaitingInfo;
        dispute.InfoDeadlineAt = now.AddDays(r.DeadlineDays);

        // The message is attributed to the admin (SenderAdminId), not to an app user: an admin id is not a user id.
        db.DisputeMessages.Add(new DisputeMessage
        {
            DisputeId = dispute.Id,
            SenderType = DisputeSenderType.Admin,
            SenderAdminId = admin.Id,
            Message = string.IsNullOrWhiteSpace(r.Note)
                ? "The operator requested more information."
                : r.Note.Trim(),
            IsInternalNote = false
        });

        audit.Record(Etamid.Domain.Admin.AuditActorType.Admin, admin.Id, "admin.dispute.request_info", "Dispute", dispute.Id,
            new { status = before.ToString() },
            new { status = dispute.Status.ToString(), deadlineDays = r.DeadlineDays, note = r.Note });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        var deal = await db.Deals.AsNoTracking().FirstAsync(d => d.Id == dispute.DealId, ct);
        // viewerId 0 = nobody: an admin id must never be compared with the buyer/seller user ids.
        return await DisputeMapper.DetailAsync(
            db, dispute, deal.BuyerId, deal.SellerId, deal.Title, deal.Amount, deal.Currency,
            0L, true, ct, files);
    }
}

// ---------- POST /admin/disputes/{id}/decide ----------

public sealed record DecideDisputeCommand(long DisputeId, string Resolution, int? SellerPercent = null, string? Note = null)
    : IRequest<Result<DisputeExecutionDto>>;

public sealed class DecideDisputeValidator : AbstractValidator<DecideDisputeCommand>
{
    public DecideDisputeValidator()
    {
        RuleFor(x => x.Resolution).MustBeRequired(40);
        RuleFor(x => x.Note!).MustBeRequired(500);
        RuleFor(x => x.SellerPercent).InclusiveBetween(1, 99).When(x => x.SellerPercent is not null);
    }
}

public sealed class DecideDisputeHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, DisputeExecutionService execution)
    : IRequestHandler<DecideDisputeCommand, Result<DisputeExecutionDto>>
{
    public async Task<Result<DisputeExecutionDto>> Handle(DecideDisputeCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(Etamid.Domain.Admin.AdminPermissionCodes.DisputesDecide, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        if (!Enum.TryParse<DisputeResolution>(r.Resolution, ignoreCase: true, out var resolution)
            || resolution is DisputeResolution.None)
            return Error.Validation("INVALID_VALUE", "resolution");
        if (resolution == DisputeResolution.Partial && r.SellerPercent is null)
            return Error.Validation("INVALID_VALUE", "sellerPercent");
        if (resolution != DisputeResolution.Partial && r.SellerPercent is not null)
            return Error.Validation("INVALID_VALUE", "sellerPercent");

        var dispute = await db.Disputes.FirstOrDefaultAsync(d => d.Id == r.DisputeId, ct);
        if (dispute is null) return DisputeErrors.NotFound;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == dispute.DealId, ct);
        if (deal is null) return DealErrors.NotFound;

        var statusBefore = dispute.Status.ToString();

        // The decision executes immediately: the money moves in this same request (execution logic unchanged).
        var executed = await execution.ExecuteAsync(
            deal, dispute, resolution, r.SellerPercent, CreatedByType.Admin, ct);
        if (executed.IsFailure) return executed.Error;

        dispute.ResolutionNote = r.Note!.Trim();
        dispute.ResolvedByAdminId = admin.Id;

        // Attributed to the admin (SenderAdminId), not to an app user: an admin id is not a user id.
        db.DisputeMessages.Add(new DisputeMessage
        {
            DisputeId = dispute.Id,
            SenderType = DisputeSenderType.Admin,
            SenderAdminId = admin.Id,
            Message = $"Decision: {ApiNames.Of(resolution)} \u2014 {r.Note.Trim()}",
            IsInternalNote = false
        });

        audit.Record(Etamid.Domain.Admin.AuditActorType.Admin, admin.Id, "admin.dispute.decide", "Dispute", dispute.Id,
            new { status = statusBefore, resolution = "None" },
            new
            {
                status = dispute.Status.ToString(),
                resolution = resolution.ToString(),
                sellerPercent = r.SellerPercent,
                sellerAmount = executed.Value.SellerAmount,
                buyerAmount = executed.Value.BuyerAmount,
                note = dispute.ResolutionNote
            });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return executed.Value;
    }
}
