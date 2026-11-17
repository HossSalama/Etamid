using Etamid.Application.AdminAuth;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Application.Notifications;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Files;
using Etamid.Domain.Money;
using Etamid.Domain.Notifications;
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

// ---------- GET /admin/disputes/{id} ----------
// The detail page used to load the list and find by id, but the list DTO has no
// reason / details / opened-by id / messages, so the page showed "—" and "#".
// This returns the full DisputeDetailDto (same shape the user-facing dispute
// endpoint returns, via the same mapper).

public sealed record GetDisputeDetailQuery(long DisputeId) : IRequest<Result<DisputeDetailDto>>;

public sealed class GetDisputeDetailHandler(IApplicationDbContext db, AdminAccessService access, IFileUrlResolver files)
    : IRequestHandler<GetDisputeDetailQuery, Result<DisputeDetailDto>>
{
    public async Task<Result<DisputeDetailDto>> Handle(GetDisputeDetailQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(Etamid.Domain.Admin.AdminPermissionCodes.DisputesView, ct);
        if (guard.IsFailure) return guard.Error;

        var dispute = await db.Disputes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == r.DisputeId, ct);
        if (dispute is null) return DisputeErrors.NotFound;

        var deal = await db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dispute.DealId, ct);
        if (deal is null) return DealErrors.NotFound;

        // viewerId 0 = nobody: an admin id must never be compared with the buyer/seller user ids.
        return await DisputeMapper.DetailAsync(
            db, dispute, deal.BuyerId, deal.SellerId, deal.Title, deal.Amount, deal.Currency,
            0L, true, ct, files);
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
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IClock clock, IFileUrlResolver files,
    NotificationService notifications)
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

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == dispute.DealId, ct);
        if (deal is not null)
        {
            // The request goes to the buyer only (design decision, 16 Nov 2026):
            // the admin asks the buyer for information; the seller is not notified.
            notifications.Create(deal.BuyerId, NotificationType.DisputeUpdate,
                "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
                dealId: deal.Id, disputeId: dispute.Id);
        }

        audit.Record(Etamid.Domain.Admin.AuditActorType.Admin, admin.Id, "admin.dispute.request_info", "Dispute", dispute.Id,
            new { status = before.ToString() },
            new { status = dispute.Status.ToString(), deadlineDays = r.DeadlineDays, note = r.Note });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        deal = await db.Deals.AsNoTracking().FirstAsync(d => d.Id == dispute.DealId, ct);
        // viewerId 0 = nobody: an admin id must never be compared with the buyer/seller user ids.
        return await DisputeMapper.DetailAsync(
            db, dispute, deal.BuyerId, deal.SellerId, deal.Title, deal.Amount, deal.Currency,
            0L, true, ct, files);
    }
}

// ---------- POST /admin/disputes/{id}/execute-agreement ----------
// The admin NEVER decides a dispute (design decision, 16 Nov 2026). The buyer
// and the seller agree between themselves, through the admin, and the admin
// only executes what they agreed — the resolution and the percent come from
// their agreement, recorded in the note. The money moves in this same request,
// exactly like the old decision path did (execution logic unchanged).

public sealed record ExecuteAgreementCommand(long DisputeId, string Resolution, int? SellerPercent = null, string? Note = null)
    : IRequest<Result<DisputeExecutionDto>>;

public sealed class ExecuteAgreementValidator : AbstractValidator<ExecuteAgreementCommand>
{
    public ExecuteAgreementValidator()
    {
        RuleFor(x => x.Resolution).MustBeRequired(40);
        RuleFor(x => x.Note!).MustBeRequired(500);
        RuleFor(x => x.SellerPercent).InclusiveBetween(1, 99).When(x => x.SellerPercent is not null);
    }
}

public sealed class ExecuteAgreementHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, DisputeExecutionService execution,
    NotificationService notifications)
    : IRequestHandler<ExecuteAgreementCommand, Result<DisputeExecutionDto>>
{
    public async Task<Result<DisputeExecutionDto>> Handle(ExecuteAgreementCommand r, CancellationToken ct)
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

        // The parties' agreement executes immediately: the money moves in this same request.
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
            Message = $"Agreement between the parties: {ApiNames.Of(resolution)} \u2014 {r.Note.Trim()}",
            IsInternalNote = false
        });

        // Both parties hear about the agreement; whoever received money hears about the wallet movement too.
        notifications.NotifyDisputeDecision(deal, dispute.Id, executed.Value.SellerAmount, executed.Value.BuyerAmount);

        audit.Record(Etamid.Domain.Admin.AuditActorType.Admin, admin.Id, "admin.dispute.execute_agreement", "Dispute", dispute.Id,
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

// ---------- POST /admin/disputes/{id}/send-documents ----------
// The dispute could not be resolved between the parties: the admin sends the
// dispute documents to the buyer so the buyer can take the case to court
// (design decision, 16 Nov 2026). This moves no money and closes nothing —
// the deal stays Disputed and the escrow stays frozen until the case is
// resolved outside the system.
// The admin uploads the documents first (POST /admin/api/uploads) and sends
// their ids here. The files are attached to an admin message with no text —
// no sentence, no note (Shahd, 16 Nov 2026): the attachment itself is the
// action, and the buyer sees the files in the thread.

public sealed record SendDisputeDocumentsCommand(long DisputeId, List<string>? FilePublicIds)
    : IRequest<Result<DisputeDetailDto>>;

public sealed class SendDisputeDocumentsValidator : AbstractValidator<SendDisputeDocumentsCommand>
{
    public SendDisputeDocumentsValidator()
    {
        RuleFor(x => x.FilePublicIds).NotNull().WithErrorCode("REQUIRED");
        RuleFor(x => x.FilePublicIds!).NotEmpty().WithErrorCode("REQUIRED");
        RuleFor(x => x.FilePublicIds!).Must(ids => ids.Count <= 5).WithErrorCode("TOO_MANY_FILES");
    }
}

public sealed class SendDisputeDocumentsHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IFileUrlResolver files,
    NotificationService notifications)
    : IRequestHandler<SendDisputeDocumentsCommand, Result<DisputeDetailDto>>
{
    public async Task<Result<DisputeDetailDto>> Handle(SendDisputeDocumentsCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(Etamid.Domain.Admin.AdminPermissionCodes.DisputesDecide, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var dispute = await db.Disputes.FirstOrDefaultAsync(d => d.Id == r.DisputeId, ct);
        if (dispute is null) return DisputeErrors.NotFound;
        if (!DisputeMapper.IsOpen(dispute.Status)) return DisputeErrors.AlreadyResolved;

        // Every id must parse, exist, and be an unattached dispute file
        // (uploaded by the admin for this purpose); otherwise nothing is sent.
        var publicIds = new List<Guid>();
        foreach (var raw in r.FilePublicIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw.Trim(), out var parsed))
                return Error.Validation("INVALID_VALUE", "filePublicIds");
            if (!publicIds.Contains(parsed)) publicIds.Add(parsed);
        }
        var stored = publicIds.Count == 0
            ? new List<StoredFile>()
            : await db.Files.Where(f => publicIds.Contains(f.PublicId)).ToListAsync(ct);
        if (stored.Count != publicIds.Count
            || stored.Any(f => f.Purpose != FilePurpose.Dispute || f.Status != FileStatus.Uploaded))
            return Error.Validation("INVALID_VALUE", "filePublicIds");

        var statusBefore = dispute.Status;

        // Attributed to the admin (SenderAdminId), not to an app user: an admin id is not a user id.
        // No text on the message — the attached files are the documents being sent.
        var message = new DisputeMessage
        {
            DisputeId = dispute.Id,
            SenderType = DisputeSenderType.Admin,
            SenderAdminId = admin.Id,
            Message = null,
            IsInternalNote = false
        };
        // The message navigation sets MessageId; DisputeId must be set by hand —
        // DetailAsync loads attachments by DisputeId, and the FK is required,
        // so a missing DisputeId would leave the file invisible (and break the
        // insert on SQL Server, which enforces the FK).
        db.DisputeMessages.Add(message);
        foreach (var file in stored)
        {
            file.Status = FileStatus.Attached;
            message.Attachments.Add(new DisputeAttachment
            {
                DisputeId = dispute.Id,
                FileId = file.Id,
                UploadedByUserId = null
            });
        }

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == dispute.DealId, ct);
        if (deal is not null)
        {
            notifications.Create(deal.BuyerId, NotificationType.DisputeUpdate,
                "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
                dealId: deal.Id, disputeId: dispute.Id);
        }

        audit.Record(Etamid.Domain.Admin.AuditActorType.Admin, admin.Id, "admin.dispute.send_documents", "Dispute", dispute.Id,
            new { status = statusBefore.ToString() },
            new { status = dispute.Status.ToString(), documentsSentToBuyer = true, fileCount = stored.Count });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        deal = await db.Deals.AsNoTracking().FirstAsync(d => d.Id == dispute.DealId, ct);
        // viewerId 0 = nobody: an admin id must never be compared with the buyer/seller user ids.
        return await DisputeMapper.DetailAsync(
            db, dispute, deal.BuyerId, deal.SellerId, deal.Title, deal.Amount, deal.Currency,
            0L, true, ct, files);
    }
}

// ---------- POST /admin/disputes/{id}/messages ----------
// The admin talks to the parties (design decision, 16 Nov 2026): the admin
// mediates — asking the seller, requesting data from the buyer — while the two
// parties never see each other's messages. An admin message is visible to both
// parties; an internal note is visible to admins only.

public sealed record PostAdminDisputeMessageCommand(long DisputeId, string? Message, bool IsInternalNote = false)
    : IRequest<Result<DisputeMessageDto>>;

public sealed class PostAdminDisputeMessageValidator : AbstractValidator<PostAdminDisputeMessageCommand>
{
    public PostAdminDisputeMessageValidator()
    {
        RuleFor(x => x.Message!).MaximumLength(1000).WithErrorCode("TOO_LONG").When(x => x.Message is not null);
    }
}

public sealed class PostAdminDisputeMessageHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IFileUrlResolver files,
    NotificationService notifications)
    : IRequestHandler<PostAdminDisputeMessageCommand, Result<DisputeMessageDto>>
{
    public async Task<Result<DisputeMessageDto>> Handle(PostAdminDisputeMessageCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(Etamid.Domain.Admin.AdminPermissionCodes.DisputesDecide, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        if (string.IsNullOrWhiteSpace(r.Message)) return Error.Validation("INVALID_VALUE", "message");

        var dispute = await db.Disputes.FirstOrDefaultAsync(d => d.Id == r.DisputeId, ct);
        if (dispute is null) return DisputeErrors.NotFound;
        if (!DisputeMapper.IsOpen(dispute.Status)) return DisputeErrors.InvalidState;

        var message = new DisputeMessage
        {
            DisputeId = dispute.Id,
            SenderType = DisputeSenderType.Admin,
            SenderAdminId = admin.Id,
            Message = r.Message.Trim(),
            IsInternalNote = r.IsInternalNote
        };
        db.DisputeMessages.Add(message);

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == dispute.DealId, ct);
        if (deal is not null && !r.IsInternalNote)
        {
            notifications.Create(deal.BuyerId, NotificationType.DisputeUpdate,
                "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
                dealId: deal.Id, disputeId: dispute.Id);
            notifications.Create(deal.SellerId, NotificationType.DisputeUpdate,
                "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
                dealId: deal.Id, disputeId: dispute.Id);
        }

        audit.Record(Etamid.Domain.Admin.AuditActorType.Admin, admin.Id, "admin.dispute.message", "Dispute", dispute.Id,
            new { status = dispute.Status.ToString() },
            new { status = dispute.Status.ToString(), isInternalNote = r.IsInternalNote });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        deal = await db.Deals.AsNoTracking().FirstAsync(d => d.Id == dispute.DealId, ct);
        // viewerId 0 = nobody: an admin id must never be compared with the buyer/seller user ids.
        var detail = await DisputeMapper.DetailAsync(
            db, dispute, deal.BuyerId, deal.SellerId, deal.Title, deal.Amount, deal.Currency,
            0L, true, ct, files);
        return detail.Messages.First(m => m.Id == message.Id);
    }
}
