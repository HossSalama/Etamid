using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Errors;
using Etamid.Application.Identity.Services;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Deals;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.Services;
using Etamid.Domain.Money;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

/// <summary>"حذف الحساب": soft delete after the password is confirmed and nothing is left open.</summary>
public sealed record DeleteAccountCommand(string Password) : IRequest<Result>;

public sealed class DeleteAccountValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountValidator() => RuleFor(x => x.Password).NotEmpty().WithErrorCode("REQUIRED");
}

public sealed class DeleteAccountHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPasswordHasher hasher,
    IClock clock,
    SessionService sessions,
    AuthAuditService audit) : IRequestHandler<DeleteAccountCommand, Result>
{
    private static readonly DealStatus[] FinishedDeal =
        [DealStatus.Released, DealStatus.Rejected, DealStatus.Cancelled, DealStatus.Expired, DealStatus.Refunded];

    private static readonly WithdrawalStatus[] OpenWithdrawal =
        [WithdrawalStatus.Pending, WithdrawalStatus.Approved, WithdrawalStatus.Processing];

    public async Task<Result> Handle(DeleteAccountCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct);
        if (user is null) return IdentityErrors.UserNotFound;

        if (!hasher.Verify(user.PasswordHash, r.Password))
            return IdentityErrors.WrongPassword;

        if (await db.Deals.AnyAsync(d => (d.BuyerId == userId || d.SellerId == userId) && !FinishedDeal.Contains(d.Status), ct))
            return IdentityErrors.AccountHasActiveDeals;

        if (await db.CompanyDeals.AnyAsync(d => (d.CreatorUserId == userId || d.CounterpartyUserId == userId)
                && (d.Status == CompanyDealStatus.Pending || d.Status == CompanyDealStatus.Active), ct))
            return IdentityErrors.AccountHasActiveCompanyDeals;

        if (await db.WithdrawalRequests.AnyAsync(w => w.UserId == userId && OpenWithdrawal.Contains(w.Status), ct))
            return IdentityErrors.AccountHasPendingWithdrawals;

        if (await db.Wallets.AnyAsync(w => w.UserId == userId && w.AvailableBalance > 0, ct))
            return IdentityErrors.AccountHasBalance;

        user.MarkDeleted(clock.UtcNow);

        // Push tokens stop receiving anything for this account.
        foreach (var device in await db.Devices.Where(d => d.UserId == userId && d.IsActive).ToListAsync(ct))
            device.IsActive = false;

        audit.Record(AuthEvent.AccountDeleted, user.Id, user.PhoneNormalized);
        await db.SaveChangesAsync(ct);

        await sessions.RevokeAllAsync(user.Id, RefreshRevokeReason.AccountDeleted, null, ct);
        return Result.Success();
    }
}
