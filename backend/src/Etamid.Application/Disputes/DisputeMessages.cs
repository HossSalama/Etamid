using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Application.Notifications;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Files;
using Etamid.Domain.Notifications;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Disputes;

// ---------- POST /deals/{id}/dispute/messages ----------

public sealed record PostDisputeMessageCommand(
    long DealId, string? Message, string? FilePublicId, bool IsInternalNote = false)
    : IRequest<Result<DisputeMessageDto>>;

public sealed class PostDisputeMessageValidator : AbstractValidator<PostDisputeMessageCommand>
{
    public PostDisputeMessageValidator()
    {
        RuleFor(x => x.Message!).MaximumLength(1000).WithErrorCode("TOO_LONG").When(x => x.Message is not null);
    }
}

public sealed class PostDisputeMessageHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files, NotificationService notifications)
    : IRequestHandler<PostDisputeMessageCommand, Result<DisputeMessageDto>>
{
    public async Task<Result<DisputeMessageDto>> Handle(PostDisputeMessageCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        if (string.IsNullOrWhiteSpace(r.Message) && string.IsNullOrWhiteSpace(r.FilePublicId))
            return Error.Validation("INVALID_VALUE", "message");

        var (deal, dispute, senderType, isOperator) = await LoadOpenDisputeAsync(db, r.DealId, userId, ct);
        if (deal is null || dispute is null) return DealErrors.NotFound;
        if (senderType is null) return DealErrors.NotFound;
        if (!DisputeMapper.IsOpen(dispute.Status)) return DisputeErrors.InvalidState;
        if (r.IsInternalNote && !isOperator) return DisputeErrors.NotAllowed;

        StoredFile? file = null;
        if (!string.IsNullOrWhiteSpace(r.FilePublicId))
        {
            if (!Guid.TryParse(r.FilePublicId.Trim(), out var publicId)) return Error.Validation("INVALID_VALUE", "filePublicId");
            file = await db.Files.FirstOrDefaultAsync(f => f.PublicId == publicId, ct);
            if (file is null || file.OwnerUserId != userId || file.Purpose != FilePurpose.Dispute
                || file.Status != FileStatus.Uploaded)
                return Error.Validation("INVALID_VALUE", "filePublicId");
        }

        var message = new DisputeMessage
        {
            DisputeId = dispute.Id,
            SenderType = senderType.Value,
            SenderUserId = userId,
            Message = string.IsNullOrWhiteSpace(r.Message) ? null : r.Message.Trim(),
            IsInternalNote = r.IsInternalNote
        };
        db.DisputeMessages.Add(message);

        // Internal notes are operator-only and never visible to the parties,
        // so they never notify anyone. A party message notifies the other party;
        // an operator message notifies both parties.
        if (!r.IsInternalNote)
        {
            if (isOperator)
            {
                notifications.Create(deal.BuyerId, NotificationType.DisputeUpdate,
                    "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
                    dealId: deal.Id, disputeId: dispute.Id);
                notifications.Create(deal.SellerId, NotificationType.DisputeUpdate,
                    "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
                    dealId: deal.Id, disputeId: dispute.Id);
            }
            else
            {
                notifications.Create(userId == deal.BuyerId ? deal.SellerId : deal.BuyerId,
                    NotificationType.DisputeUpdate,
                    "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
                    dealId: deal.Id, disputeId: dispute.Id);
            }
        }

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        if (file is not null)
        {
            file.Status = FileStatus.Attached;
            db.DisputeAttachments.Add(new DisputeAttachment
            {
                DisputeId = dispute.Id,
                MessageId = message.Id,
                FileId = file.Id,
                UploadedByUserId = userId
            });
            saved = await db.SaveGuardedAsync(ct);
            if (saved.IsFailure) return saved.Error;
        }

        // A party answering while the operator waits for information moves the dispute back to review.
        if (dispute.Status == DisputeStatus.AwaitingInfo && !isOperator)
        {
            dispute.Status = DisputeStatus.UnderReview;
            dispute.InfoDeadlineAt = null;
            saved = await db.SaveGuardedAsync(ct);
            if (saved.IsFailure) return saved.Error;
        }

        var detail = await DisputeMapper.DetailAsync(
            db, dispute, deal.BuyerId, deal.SellerId, deal.Title, deal.Amount, deal.Currency,
            userId, isOperator, ct, files);
        return detail.Messages.First(m => m.Id == message.Id);
    }

    /// <summary>Loads the deal + its open dispute for a viewer who may be a party or an operator.</summary>
    internal static async Task<(Deal? Deal, Dispute? Dispute, DisputeSenderType? SenderType, bool IsOperator)> LoadOpenDisputeAsync(
        IApplicationDbContext db, long dealId, long userId, CancellationToken ct)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == dealId, ct);
        if (deal is null) return (null, null, null, false);

        var isOperator = await DisputeOperatorPolicy.IsOperatorAsync(db, userId, ct);
        DisputeSenderType? senderType = isOperator
            ? DisputeSenderType.Admin
            : userId == deal.BuyerId ? DisputeSenderType.Buyer
            : userId == deal.SellerId ? DisputeSenderType.Seller
            : null;
        if (senderType is null) return (deal, null, null, false);

        var dispute = await db.Disputes
            .Where(d => d.DealId == deal.Id).OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (dispute is null || !DisputeMapper.IsOpen(dispute.Status)) return (deal, dispute, senderType, isOperator);

        return (deal, dispute, senderType, isOperator);
    }
}

// ---------- POST /deals/{id}/dispute/attachments ----------

public sealed record AddDisputeAttachmentCommand(long DealId, string FilePublicId, long? MessageId = null)
    : IRequest<Result<DisputeAttachmentDto>>;

public sealed class AddDisputeAttachmentValidator : AbstractValidator<AddDisputeAttachmentCommand>
{
    public AddDisputeAttachmentValidator()
    {
        RuleFor(x => x.FilePublicId).MustBeRequired(100);
    }
}

public sealed class AddDisputeAttachmentHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<AddDisputeAttachmentCommand, Result<DisputeAttachmentDto>>
{
    public async Task<Result<DisputeAttachmentDto>> Handle(AddDisputeAttachmentCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null) return DealErrors.NotFound;

        var isOperator = await DisputeOperatorPolicy.IsOperatorAsync(db, userId, ct);
        if (!deal.IsParticipant(userId) && !isOperator) return DealErrors.NotFound;

        var dispute = await db.Disputes
            .Where(d => d.DealId == deal.Id).OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (dispute is null) return DisputeErrors.NotFound;
        if (!DisputeMapper.IsOpen(dispute.Status)) return DisputeErrors.InvalidState;

        if (!Guid.TryParse(r.FilePublicId.Trim(), out var publicId)) return Error.Validation("INVALID_VALUE", "filePublicId");
        var file = await db.Files.FirstOrDefaultAsync(f => f.PublicId == publicId, ct);
        if (file is null || file.OwnerUserId != userId || file.Purpose != FilePurpose.Dispute
            || file.Status != FileStatus.Uploaded)
            return Error.Validation("INVALID_VALUE", "filePublicId");

        if (r.MessageId is { } messageId)
        {
            var messageBelongs = await db.DisputeMessages
                .AnyAsync(m => m.Id == messageId && m.DisputeId == dispute.Id, ct);
            if (!messageBelongs) return DisputeErrors.NotFound;
        }

        file.Status = FileStatus.Attached;
        var attachment = new DisputeAttachment
        {
            DisputeId = dispute.Id,
            MessageId = r.MessageId,
            FileId = file.Id,
            UploadedByUserId = userId
        };
        db.DisputeAttachments.Add(attachment);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        var uploaderName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        return new DisputeAttachmentDto(
            attachment.Id, file.Id, file.FileName, files.Resolve(file), file.ContentType, file.SizeBytes,
            uploaderName, attachment.CreatedAt);
    }
}
