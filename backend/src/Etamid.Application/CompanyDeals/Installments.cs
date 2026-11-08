using System.Text.Json;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
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

// ======================================================================= Request approval
public sealed record RequestInstallmentApprovalCommand(long DealId, long InstallmentId)
    : IRequest<Result<CompanyDealDetailDto>>;

public sealed class RequestInstallmentApprovalHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files,
    NotificationService notifications)
    : IRequestHandler<RequestInstallmentApprovalCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(RequestInstallmentApprovalCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } creatorId) return CommonErrors.Unauthorized;
        var deal = await CompanyDealSupport.FindForParticipantAsync(db, r.DealId, creatorId, ct);
        if (deal is null || (deal.CreatorUserId != creatorId && deal.CounterpartyUserId != creatorId)) return CompanyDealErrors.NotFound;
        if (deal.CreatorUserId != creatorId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Active) return CompanyDealErrors.InvalidState;

        var installment = await CompanyDealSupport.FindInstallmentAsync(db, r.DealId, r.InstallmentId, ct);
        if (installment is null) return CompanyDealErrors.InstallmentNotFound;
        if (installment.Status is not (InstallmentStatus.Scheduled or InstallmentStatus.Rejected))
            return CompanyDealErrors.InvalidState;

        // The counterparty has ApprovalResponseDays to answer (7 when unset).
        // ApprovalSendDaysBefore is the recommended window for sending the
        // request, not a hard gate: a creator asking late must still be able
        // to ask, and the deadline is what binds the counterparty.
        var now = clock.UtcNow;
        installment.Status = InstallmentStatus.AwaitingApproval;
        installment.ApprovalRequestedAt = now;
        installment.ApprovalDeadlineAt = now.AddDays(deal.ApprovalResponseDays ?? 7);

        // The counterparty is the approver: the request is theirs to answer.
        if (deal.CounterpartyUserId is { } counterpartyId)
            notifications.Create(counterpartyId, NotificationType.PaymentApproval,
                "NOTIF_PAYMENT_APPROVAL_TITLE", "NOTIF_PAYMENT_APPROVAL_BODY",
                installment.Label ?? deal.Title, companyDealId: deal.Id, installmentId: installment.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, creatorId, ct, files);
    }
}

// ======================================================================= Approve
public sealed record ApproveInstallmentCommand(long DealId, long InstallmentId)
    : IRequest<Result<CompanyDealDetailDto>>;

public sealed class ApproveInstallmentHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files,
    CompanyEscrowService escrow, NotificationService notifications)
    : IRequestHandler<ApproveInstallmentCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(ApproveInstallmentCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var deal = await CompanyDealSupport.FindForParticipantAsync(db, r.DealId, userId, ct);
        if (deal is null || (deal.CreatorUserId != userId && deal.CounterpartyUserId != userId)) return CompanyDealErrors.NotFound;
        if (deal.CounterpartyUserId != userId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Active) return CompanyDealErrors.InvalidState;

        var installment = await CompanyDealSupport.FindInstallmentAsync(db, r.DealId, r.InstallmentId, ct);
        if (installment is null) return CompanyDealErrors.InstallmentNotFound;
        if (installment.Status != InstallmentStatus.AwaitingApproval) return CompanyDealErrors.InvalidState;

        var now = clock.UtcNow;
        installment.Status = InstallmentStatus.Approved;
        installment.ApprovedAt = now;
        db.InstallmentApprovals.Add(new InstallmentApproval
        {
            InstallmentId = installment.Id,
            DecidedByUserId = userId,
            Decision = ApprovalDecision.Approved,
            DecidedAt = now
        });

        // Release on approval: when the money is already in escrow, approving
        // moves it to the creator now. When it is not funded yet, the
        // installment waits in Approved and the funding webhook releases it.
        var alreadyFunded = await CompanyDealSupport.HasSucceededPaymentAsync(db, installment.Id, ct);
        if (alreadyFunded)
        {
            var released = await escrow.StageInstallmentReleaseAsync(deal, installment, ct);
            if (released.IsFailure) return released.Error;
        }

        notifications.Create(deal.CreatorUserId, NotificationType.PaymentApproval,
            "NOTIF_PAYMENT_APPROVAL_TITLE", "NOTIF_PAYMENT_APPROVAL_BODY",
            installment.Label ?? deal.Title, companyDealId: deal.Id, installmentId: installment.Id);
        if (alreadyFunded)
            notifications.Create(deal.CreatorUserId, NotificationType.Wallet,
                "NOTIF_WALLET_RELEASE_TITLE", "NOTIF_WALLET_RELEASE_BODY",
                $"{installment.Amount} {deal.Currency} — {deal.Title}",
                companyDealId: deal.Id, installmentId: installment.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, userId, ct, files);
    }
}

// ======================================================================= Reject
public sealed record RejectInstallmentCommand(long DealId, long InstallmentId, string Reason)
    : IRequest<Result<CompanyDealDetailDto>>;

public sealed class RejectInstallmentValidator : AbstractValidator<RejectInstallmentCommand>
{
    public RejectInstallmentValidator()
        => RuleFor(x => x.Reason).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(200).WithErrorCode("TOO_LONG");
}

public sealed class RejectInstallmentHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files,
    NotificationService notifications)
    : IRequestHandler<RejectInstallmentCommand, Result<CompanyDealDetailDto>>
{
    public async Task<Result<CompanyDealDetailDto>> Handle(RejectInstallmentCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var deal = await CompanyDealSupport.FindForParticipantAsync(db, r.DealId, userId, ct);
        if (deal is null || (deal.CreatorUserId != userId && deal.CounterpartyUserId != userId)) return CompanyDealErrors.NotFound;
        if (deal.CounterpartyUserId != userId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Active) return CompanyDealErrors.InvalidState;

        var installment = await CompanyDealSupport.FindInstallmentAsync(db, r.DealId, r.InstallmentId, ct);
        if (installment is null) return CompanyDealErrors.InstallmentNotFound;
        if (installment.Status != InstallmentStatus.AwaitingApproval) return CompanyDealErrors.InvalidState;

        // A rejected installment goes back for revision: the deal and the
        // other installments are untouched, and the creator resubmits it with
        // request-approval. The reason is kept on the approval record.
        var now = clock.UtcNow;
        installment.Status = InstallmentStatus.Rejected;
        db.InstallmentApprovals.Add(new InstallmentApproval
        {
            InstallmentId = installment.Id,
            DecidedByUserId = userId,
            Decision = ApprovalDecision.Rejected,
            Reason = r.Reason,
            DecidedAt = now
        });

        notifications.Create(deal.CreatorUserId, NotificationType.PaymentApproval,
            "NOTIF_PAYMENT_APPROVAL_TITLE", "NOTIF_PAYMENT_APPROVAL_BODY",
            installment.Label ?? deal.Title, companyDealId: deal.Id, installmentId: installment.Id);

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await CompanyDealMapper.DetailAsync(db, deal, userId, ct, files);
    }
}

// ======================================================================= Fund (payment session)
public sealed record FundInstallmentCommand(long DealId, long InstallmentId, string IdempotencyKey)
    : IRequest<Result<InstallmentFundSessionDto>>;

public sealed class FundInstallmentValidator : AbstractValidator<FundInstallmentCommand>
{
    public FundInstallmentValidator()
        => RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithErrorCode("IDEMPOTENCY_KEY_REQUIRED")
            .MaximumLength(64).WithErrorCode("TOO_LONG");
}

public sealed class FundInstallmentHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IPaymentGateway gateway, IdempotencyService idempotency)
    : IRequestHandler<FundInstallmentCommand, Result<InstallmentFundSessionDto>>
{
    private sealed record StoredSession(string RedirectUrl);

    public async Task<Result<InstallmentFundSessionDto>> Handle(FundInstallmentCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var requestHash = Hashing.Sha256Hex($"company-fund:{r.DealId}:{r.InstallmentId}");
        var replay = await idempotency.FindAsync(userId, r.IdempotencyKey, requestHash, ct);
        if (replay.KeyReusedForOtherRequest) return DealErrors.IdempotencyKeyReused;
        if (replay.IsReplay)
        {
            var replayed = JsonSerializer.Deserialize<InstallmentFundSessionDto>(replay.ResponseBody!)!;
            return replayed with { Provider = string.IsNullOrWhiteSpace(replayed.Provider) ? gateway.Name : replayed.Provider };
        }

        var deal = await CompanyDealSupport.FindForParticipantAsync(db, r.DealId, userId, ct);
        if (deal is null || (deal.CreatorUserId != userId && deal.CounterpartyUserId != userId)) return CompanyDealErrors.NotFound;
        if (deal.CounterpartyUserId != userId) return CompanyDealErrors.NotAllowed;
        if (deal.Status != CompanyDealStatus.Active) return CompanyDealErrors.InvalidState;

        var installment = await CompanyDealSupport.FindInstallmentAsync(db, r.DealId, r.InstallmentId, ct);
        if (installment is null) return CompanyDealErrors.InstallmentNotFound;
        if (installment.Status == InstallmentStatus.Paid) return CompanyDealErrors.InvalidState;
        if (await CompanyDealSupport.HasSucceededPaymentAsync(db, installment.Id, ct))
            return CompanyDealErrors.InvalidState;

        // A penalty already applied to this installment is part of what the
        // payer owes, so the funding session covers amount + penalty. On
        // release the creator receives amount - fee + penalty (see
        // CompanyEscrowService): the penalty is extra money, not a deduction
        // from the installment itself.
        var penalty = await db.PenaltyCharges.AsNoTracking()
            .FirstOrDefaultAsync(p => p.InstallmentId == installment.Id && p.Status == PenaltyStatus.Applied, ct);
        var amount = installment.Amount + (penalty?.PenaltyAmount ?? 0m);

        // One open payment per installment: a retry with a new key gets the same checkout, never a second charge.
        var payment = await db.Payments
            .Where(p => p.InstallmentId == installment.Id && (p.Status == PaymentStatus.Initiated || p.Status == PaymentStatus.RequiresAction))
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        // A penalty applied after the session was opened changes what is owed:
        // the stale session is abandoned and a new one is opened for the full
        // amount, so the gateway is never asked to collect the wrong total.
        if (payment is not null && payment.Amount != amount)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureCode = "AMOUNT_CHANGED";
            payment = null;
        }

        string redirectUrl;
        if (payment is not null)
        {
            redirectUrl = JsonSerializer.Deserialize<StoredSession>(payment.ProviderPayload ?? "{}")?.RedirectUrl ?? string.Empty;
        }
        else
        {
            payment = new Payment
            {
                PublicId = Guid.NewGuid(),
                UserId = userId,
                InstallmentId = installment.Id,
                Method = PaymentMethod.BankTransfer,
                Amount = amount,
                Currency = deal.Currency,
                Status = PaymentStatus.Initiated,
                Provider = gateway.Name,
                IdempotencyKey = Hashing.Sha256Hex($"{userId}:{r.IdempotencyKey}")
            };

            var session = await gateway.CreateSessionAsync(payment.PublicId, payment.Amount, payment.Currency, deal.Id, ct);
            payment.ProviderPaymentId = session.ProviderPaymentId;
            payment.ProviderPayload = JsonSerializer.Serialize(new StoredSession(session.RedirectUrl));
            redirectUrl = session.RedirectUrl;
            db.Payments.Add(payment);
        }

        var dto = new InstallmentFundSessionDto(
            payment.PublicId, deal.Id, installment.Id, ApiNames.Of(payment.Status), payment.Amount, payment.Currency,
            payment.Provider, payment.ProviderPaymentId ?? string.Empty, redirectUrl);

        idempotency.Stage(userId, r.IdempotencyKey, "POST /company-deals/{id}/installments/{installmentId}/fund", requestHash, 200, JsonSerializer.Serialize(dto));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return dto;
    }
}
