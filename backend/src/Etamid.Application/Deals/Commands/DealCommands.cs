using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
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

namespace Etamid.Application.Deals.Commands;

// ======================================================================= Create
// Only the seller creates a deal: the seller names the buyer, picks shipping
// (and the carrier) when needed, and the buyer supplies the delivery address
// when accepting. No tracking number in v1 — delivery ends with the buyer's
// receipt confirmation.
public sealed record CreateDealCommand(
    string Title, string BuyerPhoneOrCode, string Description, decimal Amount,
    int DurationValue, string DurationUnit, bool NeedShipping,
    long? ShippingCompanyId, bool AcceptTerms,
    string? ImagePublicId = null) : IRequest<Result<DealDetailDto>>;

public sealed class CreateDealValidator : AbstractValidator<CreateDealCommand>
{
    public CreateDealValidator()
    {
        RuleFor(x => x.Title).MustBeRequired(150);
        RuleFor(x => x.BuyerPhoneOrCode).MustBeRequired(50);
        RuleFor(x => x.Description).MustBeRequired(200);
        // Minimum is the platform fee (platform_fee_amount, default 50 SAR): the
        // seller's net is Amount - fee, and Deal.Accept already rejects Amount <= fee
        // with DEAL_AMOUNT_TOO_LOW, so a deal at or below the fee could never be accepted.
        RuleFor(x => x.Amount)
            .GreaterThan(50m).WithErrorCode("DEAL_AMOUNT_INVALID")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("DEAL_AMOUNT_INVALID")
            .Must(a => decimal.Round(a, 2) == a).WithErrorCode("DEAL_AMOUNT_INVALID");
        RuleFor(x => x.DurationValue).InclusiveBetween(1, 365).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.DurationUnit)
            .NotEmpty().WithErrorCode("REQUIRED")
            .Must(u => u.Equals("hour", StringComparison.OrdinalIgnoreCase) || u.Equals("day", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.ShippingCompanyId).NotNull().WithErrorCode("REQUIRED").When(x => x.NeedShipping);
        RuleFor(x => x.AcceptTerms).Equal(true).WithErrorCode("TERMS_NOT_ACCEPTED");
        RuleFor(x => x.ImagePublicId)
            .Must(id => string.IsNullOrWhiteSpace(id) || Guid.TryParse(id.Trim(), out _))
            .WithErrorCode("INVALID_VALUE");
    }
}

public sealed class CreateDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files, NotificationService notifications)
    : IRequestHandler<CreateDealCommand, Result<DealDetailDto>>
{
    public async Task<Result<DealDetailDto>> Handle(CreateDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } sellerId) return CommonErrors.Unauthorized;

        var buyer = await UserLookup.FindByPhoneOrCodeAsync(db, r.BuyerPhoneOrCode, ct);
        if (buyer is null) return DealErrors.BuyerNotFound;
        if (buyer.Id == sellerId) return DealErrors.SameParties;

        // An image is optional, but when supplied it must be an image the seller
        // uploaded for this purpose — never another user's file, a non-image,
        // or a deleted/quarantined one. The same generic error covers all cases
        // so file ids cannot be probed.
        long? imageFileId = null;
        if (!string.IsNullOrWhiteSpace(r.ImagePublicId) && Guid.TryParse(r.ImagePublicId.Trim(), out var publicId))
        {
            var image = await db.Files.FirstOrDefaultAsync(f => f.PublicId == publicId, ct);
            if (image is null || image.OwnerUserId != sellerId || image.Purpose != FilePurpose.DealImage
                || image.Status != FileStatus.Uploaded || !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return Error.Validation("INVALID_VALUE", "imagePublicId");
            image.Status = FileStatus.Attached;
            imageFileId = image.Id;
        }

        // The seller picks the carrier when shipping; the delivery address comes
        // from the buyer at acceptance and is frozen into the deal then.
        if (r.NeedShipping)
        {
            var companyId = r.ShippingCompanyId!.Value;
            if (!await db.ShippingCompanies.AnyAsync(c => c.Id == companyId && c.IsActive, ct))
                return Error.Validation("INVALID_VALUE", "shippingCompanyId");
        }

        var settings = await DealSettings.LoadAsync(db, ct);
        var unit = r.DurationUnit.Trim().Equals("hour", StringComparison.OrdinalIgnoreCase) ? DurationUnit.Hour : DurationUnit.Day;
        var now = clock.UtcNow;

        var created = Deal.Create(
            buyer.Id, sellerId, r.Title, r.Description, r.Amount, r.DurationValue, unit,
            r.NeedShipping, r.ShippingCompanyId, null, imageFileId,
            settings.Currency, now, settings.RespondWindow);
        if (created.IsFailure) return created.Error;

        db.Deals.Add(created.Value);
        // The new deal's id is a temporary EF value until the save below;
        // the notification's DealId is fixed up to the real id with it.
        notifications.Create(created.Value.BuyerId, NotificationType.DealRequest,
            "NOTIF_DEAL_REQUEST_TITLE", "NOTIF_DEAL_REQUEST_BODY", created.Value.Title, dealId: created.Value.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, created.Value, sellerId, now, ct, files);
    }
}

// ======================================================================= Accept
// The buyer accepts — and supplies the delivery address for a shipping deal,
// which is frozen into the deal here (see Deal.Accept).
public sealed record AcceptDealCommand(long DealId, bool AcceptTerms, long? ShippingAddressId = null) : IRequest<Result<DealDetailDto>>;

public sealed class AcceptDealValidator : AbstractValidator<AcceptDealCommand>
{
    public AcceptDealValidator() => RuleFor(x => x.AcceptTerms).Equal(true).WithErrorCode("TERMS_NOT_ACCEPTED");
}

public sealed class AcceptDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files, NotificationService notifications)
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

        // The buyer supplies the delivery address here and it is frozen into the
        // deal: the snapshot is what the seller ships to, even if the buyer later
        // edits or deletes the saved address.
        long? shippingAddressId = null;
        string? shippingAddressSnapshot = null;
        if (deal.NeedShipping)
        {
            if (r.ShippingAddressId is not { } addressId)
                return Error.Validation("REQUIRED", "shippingAddressId");
            var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct);
            if (address is null)
                return Error.Validation("INVALID_VALUE", "shippingAddressId");
            shippingAddressId = addressId;
            shippingAddressSnapshot = DealMapper.ShippingSnapshotText(address);
        }

        var settings = await DealSettings.LoadAsync(db, ct);
        var now = clock.UtcNow;

        // Accept freezes price, duration and terms into the digital contract (snapshot + hash).
        var accepted = deal.Accept(userId, settings.PlatformFee, now, settings.FundingWindow, shippingAddressId, shippingAddressSnapshot);
        if (accepted.IsFailure) return accepted.Error;

        notifications.Create(deal.SellerId, NotificationType.DealAccepted,
            "NOTIF_DEAL_ACCEPTED_TITLE", "NOTIF_DEAL_ACCEPTED_BODY", deal.Title, dealId: deal.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, now, ct, files);
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

public sealed class RejectDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files, NotificationService notifications)
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

        notifications.Create(deal.SellerId, NotificationType.DealRejected,
            "NOTIF_DEAL_REJECTED_TITLE", "NOTIF_DEAL_REJECTED_BODY", deal.Title, dealId: deal.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, now, ct, files);
    }
}

// ======================================================================= Deliver
/// <summary>Seller hands over: no tracking number in v1 — delivery ends with the buyer's receipt confirmation.</summary>
public sealed record DeliverDealCommand(long DealId) : IRequest<Result<DealDetailDto>>;

public sealed class DeliverDealHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files, NotificationService notifications)
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

        if (deal.NeedShipping)
            notifications.Create(deal.BuyerId, NotificationType.DealShipped,
                "NOTIF_DEAL_SHIPPED_TITLE", "NOTIF_DEAL_SHIPPED_BODY", deal.Title, dealId: deal.Id);
        else
            notifications.Create(deal.BuyerId, NotificationType.DealDelivered,
                "NOTIF_DEAL_DELIVERED_TITLE", "NOTIF_DEAL_DELIVERED_BODY", deal.Title, dealId: deal.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return await DealMapper.DetailAsync(db, deal, userId, now, ct, files);
    }
}

// ======================================================================= Dispute
// Only the buyer opens a dispute (design decision, 16 Nov 2026): the seller
// never opens one, and the two parties never message each other — each side
// talks to the admin only. The buyer may attach up to 5 files when opening.
public sealed record OpenDisputeCommand(
    long DealId, long ReasonId, string Details, IReadOnlyList<string>? FilePublicIds = null)
    : IRequest<Result<DisputeDto>>;

public sealed class OpenDisputeValidator : AbstractValidator<OpenDisputeCommand>
{
    public OpenDisputeValidator()
    {
        RuleFor(x => x.ReasonId).GreaterThan(0).WithErrorCode("REQUIRED");
        RuleFor(x => x.Details).MustBeRequired(200);
        RuleFor(x => x.FilePublicIds)
            .Must(ids => ids is null || ids.Count <= 5).WithErrorCode("INVALID_VALUE");
    }
}

public sealed class OpenDisputeHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock, NotificationService notifications)
    : IRequestHandler<OpenDisputeCommand, Result<DisputeDto>>
{
    public async Task<Result<DisputeDto>> Handle(OpenDisputeCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;

        // Only the buyer opens a dispute (design decision, 16 Nov 2026).
        if (userId != deal.BuyerId) return DealErrors.NotAllowed;

        if (!await db.Lookups.AnyAsync(l => l.Id == r.ReasonId && l.Type == "dispute_reason" && l.IsActive, ct))
            return Error.Validation("INVALID_VALUE", "reasonId");

        // Attachments uploaded for this dispute (purpose "dispute", still unattached):
        // every id must parse, exist, and belong to the buyer, otherwise nothing opens.
        var publicIds = new List<Guid>();
        foreach (var raw in r.FilePublicIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw.Trim(), out var parsed))
                return Error.Validation("INVALID_VALUE", "filePublicIds");
            if (!publicIds.Contains(parsed)) publicIds.Add(parsed);
        }

        var files = new List<StoredFile>();
        if (publicIds.Count > 0)
        {
            files = await db.Files.Where(f => publicIds.Contains(f.PublicId)).ToListAsync(ct);
            if (files.Count != publicIds.Count
                || files.Any(f => f.OwnerUserId != userId || f.Purpose != FilePurpose.Dispute
                    || f.Status != FileStatus.Uploaded))
                return Error.Validation("INVALID_VALUE", "filePublicIds");
        }

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

        // Files attached at opening (standalone attachments, no message).
        // Attached through the dispute's navigation, so EF sets DisputeId from the
        // tracked dispute — never a temporary key value (setting DisputeId to the
        // temporary id broke the save on real SQL Server: open-with-file returned
        // INTERNAL_ERROR while open-without-file worked).
        foreach (var file in files)
        {
            file.Status = FileStatus.Attached;
            dispute.Attachments.Add(new DisputeAttachment
            {
                MessageId = null,
                FileId = file.Id,
                UploadedByUserId = userId
            });
        }

        // The dispute's id is a temporary EF value until the save below;
        // the notification's DisputeId is fixed up to the real id with it.
        notifications.Create(dispute.AgainstUserId, NotificationType.DisputeUpdate,
            "NOTIF_DISPUTE_UPDATE_TITLE", "NOTIF_DISPUTE_UPDATE_BODY", deal.Title,
            dealId: deal.Id, disputeId: dispute.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return new DisputeDto(dispute.Id, dispute.Number, deal.Id, ApiNames.Of(dispute.Status));
    }
}
