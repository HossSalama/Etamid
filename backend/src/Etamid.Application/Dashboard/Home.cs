using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals;
using Etamid.Application.Deals.Services;
using Etamid.Application.Money;
using Etamid.Domain.Deals;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Dashboard;

/// <summary>
/// Everything the mobile home screen shows, in one call: the user, the wallet
/// (same shape as GET /me/wallet) and the active deals (same shape as
/// GET /deals). Active = the New + InProgress tabs: anything not yet
/// Released / Rejected / Cancelled / Expired / Refunded.
/// </summary>
public sealed record HomeDto(
    HomeUserDto User,
    WalletDto Wallet,
    IReadOnlyList<DealSummaryDto> ActiveDeals,
    int ActiveDealsCount);

public sealed record HomeUserDto(long Id, string Name, string Code, bool NafathVerified);

public sealed record GetHomeQuery : IRequest<Result<HomeDto>>;

public sealed class GetHomeHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IClock clock, IFileUrlResolver files)
    : IRequestHandler<GetHomeQuery, Result<HomeDto>>
{
    public async Task<Result<HomeDto>> Handle(GetHomeQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new HomeUserDto(u.Id, u.Name, u.Code, u.NafathVerified))
            .FirstOrDefaultAsync(ct);
        if (user is null) return CommonErrors.Unauthorized;

        var wallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId, ct);
        var walletDto = await WalletQueries.BuildDtoAsync(db, userId, wallet, ct);

        var activeStatuses = DealActions.StatusesOf(DealCategory.New)
            .Concat(DealActions.StatusesOf(DealCategory.InProgress))
            .ToArray();
        var baseQuery = db.Deals.AsNoTracking()
            .Where(d => (d.BuyerId == userId || d.SellerId == userId) && activeStatuses.Contains(d.Status));

        var total = await baseQuery.CountAsync(ct);
        var deals = await baseQuery
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Take(20)
            .ToListAsync(ct);

        var otherIds = deals.Select(d => d.BuyerId == userId ? d.SellerId : d.BuyerId).Distinct().ToList();
        var others = await db.Users.AsNoTracking()
            .Where(u => otherIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Code })
            .ToDictionaryAsync(u => u.Id, ct);

        var stores = otherIds.Count == 0
            ? new Dictionary<long, Domain.Identity.StoreProfile>()
            : await db.StoreProfiles.AsNoTracking()
                .Where(s => otherIds.Contains(s.UserId))
                .ToDictionaryAsync(s => s.UserId, ct);

        var now = clock.UtcNow;
        var dealIds = deals.Select(d => d.Id).ToList();
        var pendingEdits = await db.DealEditRequests.AsNoTracking()
            .Where(e => dealIds.Contains(e.DealId) && e.Status == EditStatus.Pending)
            .ToDictionaryAsync(e => e.DealId, ct);

        var ratedDealIds = await db.Ratings.AsNoTracking()
            .Where(x => dealIds.Contains(x.DealId))
            .Select(x => x.DealId)
            .ToHashSetAsync(ct);

        var imageFileIds = deals.Where(d => d.ImageFileId is not null).Select(d => d.ImageFileId!.Value).Distinct().ToList();
        var imageFiles = imageFileIds.Count == 0
            ? new Dictionary<long, Domain.Files.StoredFile>()
            : await db.Files.AsNoTracking().Where(f => imageFileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        var companyIds = deals.Where(d => d.ShippingCompanyId is not null).Select(d => d.ShippingCompanyId!.Value).Distinct().ToList();
        var companies = companyIds.Count == 0
            ? new Dictionary<long, Domain.Config.ShippingCompany>()
            : await db.ShippingCompanies.AsNoTracking().Where(c => companyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var addressIds = deals.Where(d => d.ShippingAddressId is not null).Select(d => d.ShippingAddressId!.Value).Distinct().ToList();
        var addresses = addressIds.Count == 0
            ? new Dictionary<long, Domain.Identity.Address>()
            : await db.Addresses.AsNoTracking().Where(a => addressIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        IReadOnlyList<DealSummaryDto> items = deals.Select(d =>
        {
            var otherId = d.BuyerId == userId ? d.SellerId : d.BuyerId;
            others.TryGetValue(otherId, out var other);
            stores.TryGetValue(otherId, out var store);
            pendingEdits.TryGetValue(d.Id, out var pendingEdit);
            string? imageUrl = null;
            if (d.ImageFileId is { } imageFileId && imageFiles.TryGetValue(imageFileId, out var imageFile))
                imageUrl = files.Resolve(imageFile);
            companies.TryGetValue(d.ShippingCompanyId ?? -1, out var company);
            var addressText = d.ShippingAddressSnapshot;
            if (addressText is null && d.ShippingAddressId is { } addressId
                && addresses.TryGetValue(addressId, out var address))
                addressText = DealMapper.ShippingSnapshotText(address);
            return new DealSummaryDto(
                d.Id, d.Number, d.Title, d.Amount, d.Currency, ApiNames.Of(d.Status),
                DealMapper.RoleOf(d, userId), other?.Name ?? string.Empty, other?.Code ?? string.Empty,
                d.CreatedAt, DealActions.For(d, userId, now, pendingEdit, ratedDealIds.Contains(d.Id)), imageUrl,
                d.NeedShipping, company?.NameAr, company?.NameEn, addressText,
                otherId,
                DealBadge.For(d.Status, d.RespondDeadlineAt, d.ExpiresAt, d.InspectionEndsAt, now),
                store?.StoreName);
        }).ToList();

        return Result.Success(new HomeDto(user, walletDto, items, total));
    }
}
