using System.Text.Json;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Commands;

internal static class IdempotencyRules
{
    public static IRuleBuilderOptions<T, string> MustBeIdempotencyKey<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithErrorCode("IDEMPOTENCY_KEY_REQUIRED")
            .MaximumLength(64).WithErrorCode("TOO_LONG");
}

// ======================================================================= Fund (start a payment session)
public sealed record FundDealCommand(long DealId, string IdempotencyKey) : IRequest<Result<FundSessionDto>>;

public sealed class FundDealValidator : AbstractValidator<FundDealCommand>
{
    public FundDealValidator() => RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey();
}

public sealed class FundDealHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IPaymentGateway gateway, IdempotencyService idempotency)
    : IRequestHandler<FundDealCommand, Result<FundSessionDto>>
{
    private sealed record StoredSession(string RedirectUrl);

    public async Task<Result<FundSessionDto>> Handle(FundDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var requestHash = Hashing.Sha256Hex($"fund:{r.DealId}");
        var replay = await idempotency.FindAsync(userId, r.IdempotencyKey, requestHash, ct);
        if (replay.KeyReusedForOtherRequest) return DealErrors.IdempotencyKeyReused;
        if (replay.IsReplay)
        {
            var replayed = JsonSerializer.Deserialize<FundSessionDto>(replay.ResponseBody!)!;
            return replayed with { Provider = string.IsNullOrWhiteSpace(replayed.Provider) ? gateway.Name : replayed.Provider };
        }

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;
        if (userId != deal.BuyerId) return DealErrors.NotAllowed;
        if (deal.Status != DealStatus.AwaitingFunding) return DealErrors.InvalidState;
        if (deal.ExpiresAt is { } expiresAt && clock.UtcNow > expiresAt) return DealErrors.FundingExpired;
        if (!deal.IsContractIntact()) return DealErrors.ContractTampered;

        // One open payment per deal: a retry with a new key gets the same checkout, never a second charge.
        var payment = await db.Payments
            .Where(p => p.DealId == deal.Id && (p.Status == PaymentStatus.Initiated || p.Status == PaymentStatus.RequiresAction))
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

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
                DealId = deal.Id,
                Method = PaymentMethod.BankTransfer,
                Amount = deal.Amount,
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

        var dto = new FundSessionDto(
            payment.PublicId, deal.Id, ApiNames.Of(payment.Status), payment.Amount, payment.Currency,
            payment.Provider, payment.ProviderPaymentId ?? string.Empty, redirectUrl);

        idempotency.Stage(userId, r.IdempotencyKey, "POST /deals/{id}/fund", requestHash, 200, JsonSerializer.Serialize(dto));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return dto;
    }
}

// ======================================================================= Payment webhook (trusted, signed)
public sealed record ProcessPaymentWebhookCommand(string RawBody, string? Signature) : IRequest<Result<WebhookResultDto>>;

public sealed class ProcessPaymentWebhookHandler(
    IApplicationDbContext db, IClock clock, IPaymentGateway gateway, IWebhookSignatureVerifier verifier, EscrowLedgerService ledger)
    : IRequestHandler<ProcessPaymentWebhookCommand, Result<WebhookResultDto>>
{
    private const string SucceededType = "payment.succeeded";
    private const string FailedType = "payment.failed";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record WebhookPayload(string? EventId, string? Type, string? ProviderPaymentId, decimal? Amount, string? Currency);

    public async Task<Result<WebhookResultDto>> Handle(ProcessPaymentWebhookCommand r, CancellationToken ct)
    {
        // 1. Nothing is read from the body before the signature is proven.
        if (!verifier.IsValid(r.RawBody, r.Signature)) return DealErrors.WebhookSignatureInvalid;

        WebhookPayload? payload;
        try { payload = JsonSerializer.Deserialize<WebhookPayload>(r.RawBody, Json); }
        catch (JsonException) { payload = null; }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.EventId) || payload.EventId.Length > 100
            || string.IsNullOrWhiteSpace(payload.Type) || payload.Type.Length > 60
            || string.IsNullOrWhiteSpace(payload.ProviderPaymentId))
            return DealErrors.WebhookPayloadInvalid;

        var provider = gateway.Name.ToLowerInvariant();
        var eventId = payload.EventId;

        // 2. The same event delivered again changes nothing.
        var existing = await db.PaymentWebhookEvents.FirstOrDefaultAsync(e => e.Provider == provider && e.ProviderEventId == eventId, ct);
        if (existing is not null && (existing.Status == WebhookStatus.Processed || existing.Status == WebhookStatus.Ignored))
            return new WebhookResultDto("duplicate");

        // Platform ledger accounts are created before anything else is staged (creating them saves).
        if (payload.Type == SucceededType) await ledger.EnsurePlatformAccountsAsync(ct);

        try
        {
            return await ApplyAsync(payload, r.RawBody, provider, existing, ct);
        }
        catch (DbUpdateException)
        {
            // A parallel delivery of the same event won the unique index: it already did the work.
            var done = await db.PaymentWebhookEvents.AsNoTracking().AnyAsync(
                e => e.Provider == provider && e.ProviderEventId == eventId
                     && (e.Status == WebhookStatus.Processed || e.Status == WebhookStatus.Ignored), ct);
            if (done) return new WebhookResultDto("duplicate");
            throw;
        }
    }

    private async Task<Result<WebhookResultDto>> ApplyAsync(
        WebhookPayload p, string rawBody, string provider, PaymentWebhookEvent? evt, CancellationToken ct)
    {
        var now = clock.UtcNow;

        if (evt is null)
        {
            evt = new PaymentWebhookEvent
            {
                Provider = provider,
                ProviderEventId = p.EventId!,
                EventType = p.Type!,
                Payload = rawBody,
                SignatureValid = true,
                Status = WebhookStatus.Received,
                ReceivedAt = now
            };
            db.PaymentWebhookEvents.Add(evt);
        }
        evt.Attempts++;

        var payment = await db.Payments.FirstOrDefaultAsync(x => x.ProviderPaymentId == p.ProviderPaymentId, ct);
        if (payment is null)
        {
            // Possibly out of order: keep it Failed (not Processed) so the provider's retry is handled later.
            await FinishAsync(evt, WebhookStatus.Failed, "PAYMENT_NOT_FOUND", now, ct);
            return DealErrors.PaymentNotFound;
        }

        switch (p.Type)
        {
            case SucceededType:
                return await OnSucceededAsync(p, evt, payment, now, ct);

            case FailedType:
                // A failure notice that arrives after success never undoes the payment.
                if (payment.Status is PaymentStatus.Initiated or PaymentStatus.RequiresAction)
                {
                    payment.Status = PaymentStatus.Failed;
                    payment.FailureCode = "GATEWAY_DECLINED";
                    await FinishAsync(evt, WebhookStatus.Processed, null, now, ct);
                    return new WebhookResultDto("processed");
                }
                await FinishAsync(evt, WebhookStatus.Ignored, null, now, ct);
                return new WebhookResultDto("ignored");

            default:
                await FinishAsync(evt, WebhookStatus.Ignored, "UNKNOWN_EVENT_TYPE", now, ct);
                return new WebhookResultDto("ignored");
        }
    }

    private async Task<Result<WebhookResultDto>> OnSucceededAsync(
        WebhookPayload p, PaymentWebhookEvent evt, Payment payment, DateTime now, CancellationToken ct)
    {
        // Another event for a payment that is already confirmed: exactly one ledger entry per payment.
        if (payment.Status == PaymentStatus.Succeeded)
        {
            await FinishAsync(evt, WebhookStatus.Ignored, "PAYMENT_ALREADY_SUCCEEDED", now, ct);
            return new WebhookResultDto("ignored");
        }

        var amountOk = p.Amount is not { } paid || paid == payment.Amount;
        var currencyOk = p.Currency is not { } currency || currency.Equals(payment.Currency, StringComparison.OrdinalIgnoreCase);
        if (!amountOk || !currencyOk)
        {
            await FinishAsync(evt, WebhookStatus.Failed, "AMOUNT_MISMATCH", now, ct);
            return new WebhookResultDto("amount_mismatch");
        }

        var deal = payment.DealId is { } dealId ? await db.Deals.FirstOrDefaultAsync(d => d.Id == dealId, ct) : null;
        if (deal is null)
        {
            await FinishAsync(evt, WebhookStatus.Failed, "DEAL_NOT_FOUND", now, ct);
            return new WebhookResultDto("deal_not_found");
        }

        if (deal.Status != DealStatus.AwaitingFunding)
        {
            // The money did arrive but the deal can no longer take it (expired / cancelled): record it, hold nothing,
            // and leave the payment for a refund instead of inventing an escrow.
            payment.Status = PaymentStatus.Succeeded;
            await FinishAsync(evt, WebhookStatus.Ignored, $"DEAL_STATUS_{deal.Status}", now, ct);
            return new WebhookResultDto("deal_not_awaiting_funding");
        }

        var funded = deal.MarkFunded(now);
        if (funded.IsFailure)
        {
            await FinishAsync(evt, WebhookStatus.Failed, funded.Error.Code, now, ct);
            return new WebhookResultDto("failed");
        }

        payment.Status = PaymentStatus.Succeeded;
        var staged = await ledger.StageFundingAsync(deal, payment, ct);
        if (staged.IsFailure)
            return staged.Error;

        await FinishAsync(evt, WebhookStatus.Processed, null, now, ct);
        return new WebhookResultDto("processed");
    }

    private async Task FinishAsync(PaymentWebhookEvent evt, WebhookStatus status, string? error, DateTime now, CancellationToken ct)
    {
        evt.Status = status;
        evt.Error = error;
        evt.ProcessedAt = now;
        await db.SaveChangesAsync(ct);
    }
}

// ======================================================================= Release (buyer confirms receipt)
public sealed record ReleaseEscrowCommand(long DealId, string IdempotencyKey) : IRequest<Result<ReleaseResultDto>>;

public sealed class ReleaseEscrowValidator : AbstractValidator<ReleaseEscrowCommand>
{
    public ReleaseEscrowValidator() => RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey();
}

public sealed class ReleaseEscrowHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IdempotencyService idempotency, EscrowReleaseService release)
    : IRequestHandler<ReleaseEscrowCommand, Result<ReleaseResultDto>>
{
    public async Task<Result<ReleaseResultDto>> Handle(ReleaseEscrowCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var requestHash = Hashing.Sha256Hex($"release:{r.DealId}");
        var replay = await idempotency.FindAsync(userId, r.IdempotencyKey, requestHash, ct);
        if (replay.KeyReusedForOtherRequest) return DealErrors.IdempotencyKeyReused;
        if (replay.IsReplay) return JsonSerializer.Deserialize<ReleaseResultDto>(replay.ResponseBody!)!;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;
        if (userId != deal.BuyerId) return DealErrors.NotAllowed;   // the seller can never confirm receipt for the buyer

        var now = clock.UtcNow;
        var staged = await release.StageReleaseAsync(
            deal, d => d.ConfirmReceipt(userId, now), ReleaseReason.BuyerConfirmed, ct);
        if (staged.IsFailure) return staged.Error;

        var dto = new ReleaseResultDto(
            deal.Id, ApiNames.Of(deal.Status), deal.Amount, deal.FeeAmount ?? 0m, deal.SellerNetAmount ?? deal.Amount);
        idempotency.Stage(userId, r.IdempotencyKey, "POST /deals/{id}/release", requestHash, 200, JsonSerializer.Serialize(dto));

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return dto;
    }
}

// ======================================================================= Scheduled work (system, never exposed over HTTP)
public sealed record DueDealsDto(IReadOnlyList<long> ReleaseDealIds, IReadOnlyList<long> ExpireDealIds);

public sealed record GetDueDealsQuery : IRequest<Result<DueDealsDto>>;

public sealed class GetDueDealsHandler(IApplicationDbContext db, IClock clock) : IRequestHandler<GetDueDealsQuery, Result<DueDealsDto>>
{
    public async Task<Result<DueDealsDto>> Handle(GetDueDealsQuery request, CancellationToken ct)
    {
        var now = clock.UtcNow;

        // Disputed deals are never due: a dispute removes them from automatic release.
        var release = await db.Deals.AsNoTracking()
            .Where(d => d.Status == DealStatus.Inspection && !d.IsDisputed && d.InspectionEndsAt != null && d.InspectionEndsAt <= now)
            .OrderBy(d => d.InspectionEndsAt).Select(d => d.Id).Take(50).ToListAsync(ct);

        var expire = await db.Deals.AsNoTracking()
            .Where(d => (d.Status == DealStatus.PendingAcceptance || d.Status == DealStatus.AwaitingFunding)
                        && d.ExpiresAt != null && d.ExpiresAt < now)
            .OrderBy(d => d.ExpiresAt).Select(d => d.Id).Take(50).ToListAsync(ct);

        return new DueDealsDto(release, expire);
    }
}

public sealed record AutoReleaseDealCommand(long DealId) : IRequest<Result>;

public sealed class AutoReleaseDealHandler(IApplicationDbContext db, IClock clock, EscrowReleaseService release)
    : IRequestHandler<AutoReleaseDealCommand, Result>
{
    public async Task<Result> Handle(AutoReleaseDealCommand r, CancellationToken ct)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null) return DealErrors.NotFound;

        var now = clock.UtcNow;
        var staged = await release.StageReleaseAsync(deal, d => d.AutoRelease(now), ReleaseReason.AutoRelease, ct);
        if (staged.IsFailure) return staged;

        return await db.SaveGuardedAsync(ct);
    }
}

public sealed record ExpireDealCommand(long DealId) : IRequest<Result>;

public sealed class ExpireDealHandler(IApplicationDbContext db, IClock clock) : IRequestHandler<ExpireDealCommand, Result>
{
    public async Task<Result> Handle(ExpireDealCommand r, CancellationToken ct)
    {
        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null) return DealErrors.NotFound;

        var expired = deal.Expire(clock.UtcNow);
        if (expired.IsFailure) return expired;

        return await db.SaveGuardedAsync(ct);
    }
}
