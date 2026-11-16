using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Disputes;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Disputes;

// ---------- DTOs ----------

public sealed record DisputeAttachmentDto(
    long Id, long FileId, string FileName, string? FileUrl, string ContentType, long SizeBytes,
    string? UploadedByName, DateTime CreatedAt);

public sealed record DisputeMessageDto(
    long Id, string SenderType, string? SenderName, string? Message, bool IsInternalNote,
    DateTime CreatedAt, IReadOnlyList<DisputeAttachmentDto> Attachments);

public sealed record SettlementProposalDto(
    long Id, long ProposedById, string? ProposedByName, string Resolution, int? SellerPercent,
    string Status, DateTime CreatedAt, DateTime? DecidedAt);

public sealed record DisputeDetailDto(
    long Id, string Number, long DealId, string DealTitle, decimal DealAmount, string DealCurrency,
    string Status, string Resolution, string? ResolutionNote, string Details,
    long ReasonId, string? ReasonNameAr, string? ReasonNameEn,
    long OpenedById, string? OpenedByName, long AgainstUserId, string? AgainstName, string Role,
    DateTime? InfoDeadlineAt, DateTime? ResolvedAt, DateTime CreatedAt,
    IReadOnlyList<DisputeMessageDto> Messages, IReadOnlyList<DisputeAttachmentDto> Attachments,
    SettlementProposalDto? OpenProposal, IReadOnlyList<string> AvailableActions);

public sealed record DisputeSummaryDto(
    long Id, string Number, long DealId, string DealTitle, decimal DealAmount,
    string Status, string Resolution, string? OpenedByName, DateTime CreatedAt, DateTime? ResolvedAt);

public sealed record DisputeExecutionDto(
    long DisputeId, string Number, string Resolution,
    decimal SellerAmount, decimal BuyerAmount, decimal FeeAmount, string DealStatus);

// ---------- Mapper ----------

/// <summary>
/// Shapes disputes for the API. Operator-only internal notes are included only when
/// the viewer is an operator; parties never see them.
/// </summary>
public static class DisputeMapper
{
    public static bool IsOpen(DisputeStatus status)
        => status is DisputeStatus.UnderReview or DisputeStatus.AwaitingInfo;

    public static string RoleOf(long dealBuyerId, long dealSellerId, long viewerId)
        => viewerId == dealBuyerId ? "buyer" : viewerId == dealSellerId ? "seller" : "operator";

    // A party talks to the admin only (design decision, 16 Nov 2026): the buyer
    // and the seller never see each other's messages or attachments, and there
    // is no settlement proposal between them any more — the two sides agree
    // through the admin, who executes their agreement (see DisputeAdmin).
    public static IReadOnlyList<string> AvailableActions(
        Dispute dispute, long dealBuyerId, long dealSellerId, long viewerId)
    {
        if (!IsOpen(dispute.Status)) return [];
        if (viewerId == dealBuyerId || viewerId == dealSellerId)
            return ["send_message"];
        return [];
    }

    public static async Task<DisputeDetailDto> DetailAsync(
        IApplicationDbContext db, Dispute dispute, long dealBuyerId, long dealSellerId,
        string dealTitle, decimal dealAmount, string dealCurrency,
        long viewerId, bool includeInternal, CancellationToken ct, IFileUrlResolver? files = null)
    {
        var messages = await db.DisputeMessages.AsNoTracking()
            .Where(m => m.DisputeId == dispute.Id).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);
        var attachments = await db.DisputeAttachments.AsNoTracking()
            .Where(a => a.DisputeId == dispute.Id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(ct);

        var userIds = new HashSet<long> { dispute.OpenedById, dispute.AgainstUserId };
        foreach (var m in messages)
            if (m.SenderUserId is { } senderId) userIds.Add(senderId);
        foreach (var a in attachments)
            if (a.UploadedByUserId is { } uploaderId) userIds.Add(uploaderId);
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var fileIds = attachments.Select(a => a.FileId).Distinct().ToList();
        var storedFiles = fileIds.Count == 0
            ? new Dictionary<long, (string FileName, string? Url, string ContentType, long SizeBytes)>()
            : (await db.Files.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToListAsync(ct))
                .ToDictionary(f => f.Id, f => (FileName: f.FileName, Url: files?.Resolve(f), ContentType: f.ContentType, SizeBytes: f.SizeBytes));

        var reason = await db.Lookups.AsNoTracking()
            .Where(l => l.Id == dispute.ReasonId).Select(l => new { l.NameAr, l.NameEn }).FirstOrDefaultAsync(ct);

        string? NameOf(long id) => users.TryGetValue(id, out var name) ? name : null;

        DisputeAttachmentDto MapAttachment(DisputeAttachment a)
        {
            var uploaderName = a.UploadedByUserId is { } uid ? NameOf(uid) : null;
            if (!storedFiles.TryGetValue(a.FileId, out var f))
                return new DisputeAttachmentDto(
                    a.Id, a.FileId, string.Empty, null, string.Empty, 0,
                    uploaderName, a.CreatedAt);
            return new DisputeAttachmentDto(
                a.Id, a.FileId, f.FileName, f.Url, f.ContentType, f.SizeBytes,
                uploaderName, a.CreatedAt);
        }

        // Admin/operator view (includeInternal): everything. A party sees only
        // the admin's messages and its own — never the other party's.
        // The seller sees no thread at all (Shahd, 16 Nov 2026): only that a
        // dispute exists, plus WHAT it is about — the reason and the details
        // (Shahd, 16 Nov 2026, "يظهر له تفاصيل النزاع (السبب والتفاصيل)").
        // The buyer talks to the admin; the seller is told separately, so
        // the seller gets no messages, no attachments, and no actions.
        var viewerRole = RoleOf(dealBuyerId, dealSellerId, viewerId);
        var sellerView = !includeInternal && viewerRole == "seller";
        var visibleMessages = sellerView
            ? new List<DisputeMessage>()
            : includeInternal
                ? messages
                : messages.Where(m => !m.IsInternalNote
                    && (m.SenderType == DisputeSenderType.Admin
                        || (viewerRole == "buyer" && m.SenderType == DisputeSenderType.Buyer)
                        || (viewerRole == "seller" && m.SenderType == DisputeSenderType.Seller))).ToList();
        var messageDtos = visibleMessages.Select(m => new DisputeMessageDto(
            m.Id, ApiNames.Of(m.SenderType),
            m.SenderUserId is { } senderId ? NameOf(senderId) : null,
            m.Message, m.IsInternalNote, m.CreatedAt,
            attachments.Where(a => a.MessageId == m.Id).Select(MapAttachment).ToList())).ToList();

        // Standalone attachments follow the same rule: a party sees its own and
        // the operator's, never the other party's. Message attachments are only
        // shown with their (visible) message, via MapAttachment above.
        // The seller sees none of them (see above).
        var standalone = sellerView
            ? new List<DisputeAttachmentDto>()
            : attachments
                .Where(a => a.MessageId is null
                    && (includeInternal
                        || a.UploadedByUserId is null
                        || a.UploadedByUserId == viewerId
                        || (a.UploadedByUserId != dealBuyerId && a.UploadedByUserId != dealSellerId)))
                .Select(MapAttachment).ToList();

        // No settlement proposals any more (16 Nov 2026): the field stays in the
        // DTO as null so older clients do not break.
        SettlementProposalDto? proposalDto = null;

        var actions = sellerView
            ? new List<string>()
            : AvailableActions(dispute, dealBuyerId, dealSellerId, viewerId);

        return new DisputeDetailDto(
            dispute.Id, dispute.Number, dispute.DealId, dealTitle, dealAmount, dealCurrency,
            ApiNames.Of(dispute.Status), ApiNames.Of(dispute.Resolution), dispute.ResolutionNote,
            dispute.Details,
            dispute.ReasonId,
            reason?.NameAr, reason?.NameEn,
            dispute.OpenedById, NameOf(dispute.OpenedById), dispute.AgainstUserId, NameOf(dispute.AgainstUserId),
            RoleOf(dealBuyerId, dealSellerId, viewerId),
            dispute.InfoDeadlineAt, dispute.ResolvedAt, dispute.CreatedAt,
            messageDtos, standalone, proposalDto, actions);
    }

    public static async Task<DisputeSummaryDto> SummaryAsync(
        IApplicationDbContext db, Dispute dispute, CancellationToken ct)
    {
        var deal = await db.Deals.AsNoTracking()
            .Where(d => d.Id == dispute.DealId).Select(d => new { d.Title, d.Amount }).FirstOrDefaultAsync(ct);
        var opener = await db.Users.AsNoTracking()
            .Where(u => u.Id == dispute.OpenedById).Select(u => u.Name).FirstOrDefaultAsync(ct);
        return new DisputeSummaryDto(
            dispute.Id, dispute.Number, dispute.DealId, deal?.Title ?? string.Empty, deal?.Amount ?? 0m,
            ApiNames.Of(dispute.Status), ApiNames.Of(dispute.Resolution), opener,
            dispute.CreatedAt, dispute.ResolvedAt);
    }
}
