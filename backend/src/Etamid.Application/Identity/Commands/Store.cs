using Etamid.Application.Common;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record StoreDto(
    long Id, string Name, long? CategoryId, string? Category, string? BannerUrl, decimal Rating, int SuccessfulDeals);

internal static class StoreMapper
{
    public static async Task<StoreDto> BuildAsync(
        IApplicationDbContext db, IFileUrlResolver urls, StoreProfile store, CancellationToken ct)
    {
        string? category = null;
        if (store.CategoryId is { } categoryId)
        {
            var lookup = await db.Lookups.AsNoTracking().FirstOrDefaultAsync(l => l.Id == categoryId, ct);
            if (lookup is not null) category = LanguageHelper.Pick(lookup.NameAr, lookup.NameEn);
        }

        string? banner = null;
        if (store.BannerFileId is { } bannerId)
            banner = urls.Resolve(await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == bannerId, ct));

        return new StoreDto(store.UserId, store.StoreName, store.CategoryId, category, banner, store.RatingAvg, store.SuccessfulDeals);
    }
}

public sealed record GetMyStoreQuery : IRequest<Result<StoreDto?>>;

public sealed class GetMyStoreHandler(IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<GetMyStoreQuery, Result<StoreDto?>>
{
    public async Task<Result<StoreDto?>> Handle(GetMyStoreQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var store = await db.StoreProfiles.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (store is null) return Result.Success<StoreDto?>(null);

        return Result.Success<StoreDto?>(await StoreMapper.BuildAsync(db, urls, store, ct));
    }
}

public sealed record SaveMyStoreCommand(string StoreName, long? CategoryId, string? BannerFileId) : IRequest<Result<StoreDto?>>;

public sealed class SaveMyStoreValidator : AbstractValidator<SaveMyStoreCommand>
{
    public SaveMyStoreValidator()
    {
        RuleFor(x => x.StoreName).MustBeRequired(150);
        RuleFor(x => x.BannerFileId!)
            .Must(v => Guid.TryParse(v, out _)).WithErrorCode("INVALID_VALUE")
            .When(x => !string.IsNullOrWhiteSpace(x.BannerFileId));
    }
}

public sealed class SaveMyStoreHandler(IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<SaveMyStoreCommand, Result<StoreDto?>>
{
    public async Task<Result<StoreDto?>> Handle(SaveMyStoreCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        if (r.CategoryId is { } categoryId &&
            !await db.Lookups.AnyAsync(l => l.Id == categoryId && l.Type == "store_category" && l.IsActive, ct))
            return Error.Validation("INVALID_VALUE", "categoryId");

        StoredFile? banner = null;
        if (!string.IsNullOrWhiteSpace(r.BannerFileId))
        {
            var publicId = Guid.Parse(r.BannerFileId);
            banner = await db.Files.FirstOrDefaultAsync(
                f => f.PublicId == publicId && f.OwnerUserId == userId && f.Status != FileStatus.Deleted, ct);
            if (banner is null || !banner.ContentType.StartsWith("image/", StringComparison.Ordinal))
                return Error.Validation("INVALID_VALUE", "bannerFileId");
        }

        var store = await db.StoreProfiles.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (store is null)
        {
            store = new StoreProfile { UserId = userId };
            db.StoreProfiles.Add(store);
        }

        store.StoreName = r.StoreName.Trim();
        store.CategoryId = r.CategoryId;
        store.BannerFileId = banner?.Id;
        if (banner is not null) banner.Status = FileStatus.Attached;

        await db.SaveChangesAsync(ct);
        return Result.Success<StoreDto?>(await StoreMapper.BuildAsync(db, urls, store, ct));
    }
}
