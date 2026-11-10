using Etamid.Application.AdminAuth;
using Etamid.Domain.Admin;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Money;

// ---------- DTOs ----------

/// <summary>
/// Balances are null while the wallet is locked: activation gates the whole
/// wallet, so nothing financial is visible before it — not even a zero.
/// </summary>
public sealed record WalletDto(
    bool Activated, DateTime? ActivatedAt, decimal? AvailableBalance, decimal? EscrowHeld,
    bool HasBankAccount, string? BankAccountMasked);

public sealed record WalletTransactionDto(
    long Id, string Type, string Direction, decimal Amount, decimal? BalanceAfter,
    long? DealId, string? Description, DateTime CreatedAt);

public sealed record WithdrawalDto(
    long Id, decimal Amount, decimal FeeAmount, string IbanSnapshotMasked, string Status,
    string? RejectionReason, string? BankReference, DateTime? ApprovedAt, DateTime? ProcessedAt, DateTime CreatedAt);

public sealed record WithdrawalAdminDto(
    long Id, long UserId, string UserName, decimal Amount, decimal FeeAmount, string IbanSnapshotMasked, string Status,
    string? RejectionReason, string? BankReference, DateTime? ApprovedAt, DateTime? ProcessedAt, DateTime CreatedAt);

// ---------- GET /me/wallet ----------

public sealed record GetMyWalletQuery : IRequest<Result<WalletDto>>;

public sealed class GetMyWalletHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMyWalletQuery, Result<WalletDto>>
{
    public async Task<Result<WalletDto>> Handle(GetMyWalletQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var wallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId, ct);
        return Result.Success(await WalletQueries.BuildDtoAsync(db, userId, wallet, ct));
    }
}

// ---------- POST /me/wallet/activate ----------

public sealed record ActivateWalletCommand : IRequest<Result<WalletDto>>;

public sealed class ActivateWalletHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, EscrowLedgerService ledger)
    : IRequestHandler<ActivateWalletCommand, Result<WalletDto>>
{
    public async Task<Result<WalletDto>> Handle(ActivateWalletCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        // Idempotent: activating an already-active wallet just returns it.
        var wallet = await ledger.GetOrCreateWalletAsync(userId, ct);
        if (!wallet.Activated)
        {
            wallet.Activated = true;
            wallet.ActivatedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success(await WalletQueries.BuildDtoAsync(db, userId, wallet, ct));
    }
}

// ---------- GET /me/wallet/transactions ----------

public sealed record GetWalletTransactionsQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<WalletTransactionDto>>>;

public sealed class GetWalletTransactionsHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetWalletTransactionsQuery, Result<PagedResult<WalletTransactionDto>>>
{
    public async Task<Result<PagedResult<WalletTransactionDto>>> Handle(GetWalletTransactionsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var wallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet is null || !wallet.Activated) return WalletErrors.NotActivated;

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);
        var query = db.LedgerEntries.AsNoTracking().Where(e => e.AccountId == wallet.LedgerAccountId);
        var total = await query.CountAsync(ct);
        var entries = await query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var txIds = entries.Select(e => e.TransactionId).Distinct().ToList();
        var transactions = await db.LedgerTransactions.AsNoTracking()
            .Where(t => txIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

        var items = entries.Select(e =>
        {
            var tx = transactions.GetValueOrDefault(e.TransactionId);
            return new WalletTransactionDto(
                e.Id,
                tx is null ? string.Empty : ApiNames.Of(tx.Type),
                ApiNames.Of(e.Direction),
                e.Amount, e.BalanceAfter,
                tx?.DealId, tx?.Description, e.CreatedAt);
        }).ToList();
        return new PagedResult<WalletTransactionDto>(items, page, pageSize, total);
    }
}

// ---------- POST /me/wallet/withdrawals ----------

public sealed record RequestWithdrawalCommand(decimal Amount, string IdempotencyKey)
    : IRequest<Result<WithdrawalDto>>;

public sealed class RequestWithdrawalValidator : AbstractValidator<RequestWithdrawalCommand>
{
    public RequestWithdrawalValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithErrorCode("INVALID_VALUE")
            .LessThanOrEqualTo(10_000_000m).WithErrorCode("INVALID_VALUE")
            .Must(a => decimal.Round(a, 2) == a).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(100).WithErrorCode("TOO_LONG");
    }
}

public sealed class RequestWithdrawalHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<RequestWithdrawalCommand, Result<WithdrawalDto>>
{
    public async Task<Result<WithdrawalDto>> Handle(RequestWithdrawalCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet is null || !wallet.Activated) return WalletErrors.NotActivated;

        // A replayed idempotency key returns the original request, never a second one.
        var replayed = await db.WithdrawalRequests.AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.IdempotencyKey == r.IdempotencyKey, ct);
        if (replayed is not null) return Result.Success(WalletQueries.ToDto(replayed));

        var bank = await db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (bank is null) return WalletErrors.BankAccountRequired;

        // Pending + approved requests already earmark money that has not left
        // the wallet yet, so they count against the balance together with this one.
        var earmarked = await db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.UserId == userId && (w.Status == WithdrawalStatus.Pending || w.Status == WithdrawalStatus.Approved))
            .SumAsync(w => w.Amount, ct);
        if (earmarked + r.Amount > wallet.AvailableBalance) return WalletErrors.InsufficientBalance;

        var request = new WithdrawalRequest
        {
            UserId = userId,
            WalletId = wallet.Id,
            BankAccountId = bank.Id,
            IbanSnapshotMasked = "SA" + new string('•', 9) + bank.IbanLast4,
            Amount = r.Amount,
            FeeAmount = 0m,
            Status = WithdrawalStatus.Pending,
            IdempotencyKey = r.IdempotencyKey
        };
        db.WithdrawalRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return Result.Success(WalletQueries.ToDto(request));
    }
}

// ---------- GET /me/wallet/withdrawals ----------

public sealed record GetMyWithdrawalsQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<WithdrawalDto>>>;

public sealed class GetMyWithdrawalsHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMyWithdrawalsQuery, Result<PagedResult<WithdrawalDto>>>
{
    public async Task<Result<PagedResult<WithdrawalDto>>> Handle(GetMyWithdrawalsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var wallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet is null || !wallet.Activated) return WalletErrors.NotActivated;

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);
        var query = db.WithdrawalRequests.AsNoTracking().Where(w => w.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<WithdrawalDto>(items.Select(WalletQueries.ToDto).ToList(), page, pageSize, total);
    }
}

// ---------- Admin: GET /admin/withdrawals ----------
// Cycle 9 phase 2: these endpoints are gated by an AdminJwt identity plus a permission checked from the database
// (AdminAccessService).

public sealed record ListWithdrawalsQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<WithdrawalAdminDto>>>;

public sealed class ListWithdrawalsHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListWithdrawalsQuery, Result<PagedResult<WithdrawalAdminDto>>>
{
    public async Task<Result<PagedResult<WithdrawalAdminDto>>> Handle(ListWithdrawalsQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.WithdrawalsView, ct);
        if (guard.IsFailure) return guard.Error;

        WithdrawalStatus? status = null;
        if (!string.IsNullOrWhiteSpace(r.Status))
        {
            if (!Enum.TryParse<WithdrawalStatus>(r.Status, ignoreCase: true, out var parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);
        var query = db.WithdrawalRequests.AsNoTracking();
        if (status is not null) query = query.Where(w => w.Status == status);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var userIds = items.Select(w => w.UserId).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var dtos = items.Select(w => new WithdrawalAdminDto(
            w.Id, w.UserId, names.GetValueOrDefault(w.UserId, string.Empty),
            w.Amount, w.FeeAmount, w.IbanSnapshotMasked, ApiNames.Of(w.Status),
            w.RejectionReason, w.BankReference, w.ApprovedAt, w.ProcessedAt, w.CreatedAt)).ToList();
        return new PagedResult<WithdrawalAdminDto>(dtos, page, pageSize, total);
    }
}

// ---------- Admin: approve / reject / complete ----------

public sealed record ApproveWithdrawalCommand(long Id) : IRequest<Result<WithdrawalDto>>;

public sealed class ApproveWithdrawalHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IClock clock)
    : IRequestHandler<ApproveWithdrawalCommand, Result<WithdrawalDto>>
{
    public async Task<Result<WithdrawalDto>> Handle(ApproveWithdrawalCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.WithdrawalsApprove, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var request = await db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Id == r.Id, ct);
        if (request is null) return WalletErrors.WithdrawalNotFound;
        if (request.Status != WithdrawalStatus.Pending) return WalletErrors.InvalidState;

        // Re-checked at approval: the balance may have moved since the request.
        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct);
        if (wallet is null || wallet.AvailableBalance < request.Amount) return WalletErrors.InsufficientBalance;

        var before = request.Status;
        request.Status = WithdrawalStatus.Approved;
        request.ApprovedAt = clock.UtcNow;
        request.ApprovedByAdminId = admin.Id;
        audit.Record(AuditActorType.Admin, admin.Id, "admin.withdrawal.approve", "WithdrawalRequest", request.Id,
            new { status = before.ToString() }, new { status = request.Status.ToString() });
        await db.SaveChangesAsync(ct);
        return Result.Success(WalletQueries.ToDto(request));
    }
}

public sealed record RejectWithdrawalCommand(long Id, string Reason) : IRequest<Result<WithdrawalDto>>;

public sealed class RejectWithdrawalValidator : AbstractValidator<RejectWithdrawalCommand>
{
    public RejectWithdrawalValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(500).WithErrorCode("TOO_LONG");
    }
}

/// <summary>Reject is covered by admin.withdrawals.approve (there is no separate reject permission code).</summary>
public sealed class RejectWithdrawalHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit)
    : IRequestHandler<RejectWithdrawalCommand, Result<WithdrawalDto>>
{
    public async Task<Result<WithdrawalDto>> Handle(RejectWithdrawalCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.WithdrawalsApprove, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var request = await db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Id == r.Id, ct);
        if (request is null) return WalletErrors.WithdrawalNotFound;
        if (request.Status != WithdrawalStatus.Pending) return WalletErrors.InvalidState;

        var before = request.Status;
        request.Status = WithdrawalStatus.Rejected;
        request.RejectionReason = r.Reason;
        request.ApprovedByAdminId = admin.Id;
        audit.Record(AuditActorType.Admin, admin.Id, "admin.withdrawal.reject", "WithdrawalRequest", request.Id,
            new { status = before.ToString() }, new { status = request.Status.ToString(), reason = r.Reason });
        await db.SaveChangesAsync(ct);
        return Result.Success(WalletQueries.ToDto(request));
    }
}

public sealed record CompleteWithdrawalCommand(long Id, string BankReference) : IRequest<Result<WithdrawalDto>>;

public sealed class CompleteWithdrawalValidator : AbstractValidator<CompleteWithdrawalCommand>
{
    public CompleteWithdrawalValidator()
    {
        RuleFor(x => x.BankReference).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(100).WithErrorCode("TOO_LONG");
    }
}

public sealed class CompleteWithdrawalHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IClock clock, EscrowLedgerService ledger)
    : IRequestHandler<CompleteWithdrawalCommand, Result<WithdrawalDto>>
{
    public async Task<Result<WithdrawalDto>> Handle(CompleteWithdrawalCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.WithdrawalsComplete, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var request = await db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Id == r.Id, ct);
        if (request is null) return WalletErrors.WithdrawalNotFound;
        if (request.Status != WithdrawalStatus.Approved) return WalletErrors.InvalidState;

        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct);
        if (wallet is null || wallet.AvailableBalance < request.Amount) return WalletErrors.InsufficientBalance;

        // The money leaves the wallet only here, when the operator confirms
        // the manual bank transfer: Dr user wallet / Cr BankPayout, balanced.
        var payout = await ledger.GetPlatformAccountAsync(LedgerAccountType.BankPayout, ct);
        var lines = new List<PostingLine>
        {
            new(wallet.LedgerAccountId, LedgerDirection.Debit, request.Amount),
            new(payout, LedgerDirection.Credit, request.Amount)
        };
        if (LedgerPosting.Validate(lines).IsFailure) return DealErrors.LedgerUnbalanced;

        var key = $"withdrawal:{request.Id}";
        if (await db.LedgerTransactions.AnyAsync(t => t.IdempotencyKey == key, ct)) return WalletErrors.InvalidState;

        var committed = await db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId == wallet.LedgerAccountId)
            .SumAsync(e => e.Direction == LedgerDirection.Credit ? e.Amount : -e.Amount, ct);
        var balanceAfter = committed - request.Amount;

        var transaction = new LedgerTransaction
        {
            PublicId = Guid.NewGuid(),
            Type = LedgerTransactionType.Withdrawal,
            WithdrawalId = request.Id,
            Description = $"Withdrawal {request.Id} to bank account",
            IdempotencyKey = key,
            CreatedByType = CreatedByType.Admin,
            CreatedById = admin.Id
        };
        transaction.Entries.Add(new LedgerEntry
        {
            AccountId = wallet.LedgerAccountId, Direction = LedgerDirection.Debit,
            Amount = request.Amount, BalanceAfter = balanceAfter
        });
        transaction.Entries.Add(new LedgerEntry
        {
            AccountId = payout, Direction = LedgerDirection.Credit, Amount = request.Amount
        });
        db.LedgerTransactions.Add(transaction);

        var before = request.Status;
        wallet.AvailableBalance = balanceAfter;
        request.Status = WithdrawalStatus.Completed;
        request.ProcessedAt = clock.UtcNow;
        request.BankReference = r.BankReference;
        // The approver stays recorded: only fill the field when nobody did (the completing admin is in the audit row).
        request.ApprovedByAdminId ??= admin.Id;
        audit.Record(AuditActorType.Admin, admin.Id, "admin.withdrawal.complete", "WithdrawalRequest", request.Id,
            new { status = before.ToString() }, new { status = request.Status.ToString(), bankReference = r.BankReference });
        await db.SaveChangesAsync(ct);
        return Result.Success(WalletQueries.ToDto(request));
    }
}

// ---------- Shared ----------

public static class WalletQueries
{
    /// <summary>
    /// Escrow-held, computed from deals (never from the ledger): a buyer's
    /// money still in escrow, and a seller's money still awaiting release.
    /// A disputed hold counts for both sides (it is frozen, not gone); a
    /// refund-pending deal counts only for the buyer (it is on its way back).
    /// </summary>
    public static async Task<WalletDto> BuildDtoAsync(
        IApplicationDbContext db, long userId, Wallet? wallet, CancellationToken ct)
    {
        var bank = await db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId, ct);
        var masked = bank is null ? null : "SA" + new string('•', 9) + bank.IbanLast4;
        if (wallet is null || !wallet.Activated)
            return new WalletDto(false, wallet?.ActivatedAt, null, null, bank is not null, masked);

        var buyerHeld = new[] { DealStatus.Funded, DealStatus.InDelivery, DealStatus.Inspection, DealStatus.Disputed, DealStatus.RefundPending };
        var sellerHeld = new[] { DealStatus.Funded, DealStatus.InDelivery, DealStatus.Inspection, DealStatus.Disputed };
        var held = await db.Deals.AsNoTracking()
            .Where(d => (d.BuyerId == userId && buyerHeld.Contains(d.Status))
                || (d.SellerId == userId && sellerHeld.Contains(d.Status)))
            .SumAsync(d => d.Amount, ct);
        return new WalletDto(true, wallet.ActivatedAt, wallet.AvailableBalance, held, bank is not null, masked);
    }

    public static WithdrawalDto ToDto(WithdrawalRequest w) => new(
        w.Id, w.Amount, w.FeeAmount, w.IbanSnapshotMasked, ApiNames.Of(w.Status),
        w.RejectionReason, w.BankReference, w.ApprovedAt, w.ProcessedAt, w.CreatedAt);
}
