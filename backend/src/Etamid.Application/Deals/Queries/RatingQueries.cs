using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Domain.Files;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Queries;

/// <summary>One visible review on a seller's page: stars, comment, the deal it came from, and the rater's display name, avatar URL and Nafath-verified flag.</summary>
public sealed record SellerReviewDto(
    long Id, long DealId, string DealTitle, int Stars, string? Comment, string RaterName,
    string? RaterAvatarUrl, bool RaterVerified, DateTime CreatedAt);

public sealed record GetSellerReviewsQuery(long SellerId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<SellerReviewDto>>>;

public sealed class GetSellerReviewsHandler(IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<GetSellerReviewsQuery, Result<PagedResult<SellerReviewDto>>>
{
    public async Task<Result<PagedResult<SellerReviewDto>>> Handle(GetSellerReviewsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } _) return CommonErrors.Unauthorized;

        var sellerExists = await db.Users.AnyAsync(u => u.Id == r.SellerId, ct);
        if (!sellerExists) return CommonErrors.NotFound;

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.Ratings.AsNoTracking().Where(x => x.SellerId == r.SellerId && x.IsVisible);
        var total = await query.CountAsync(ct);
        var ratings = await query
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        var dealIds = ratings.Select(x => x.DealId).Distinct().ToList();
        var deals = await db.Deals.AsNoTracking().Where(d => dealIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Title, ct);
        var raterIds = ratings.Select(x => x.RaterId).Distinct().ToList();
        var raters = await db.Users.AsNoTracking().Where(u => raterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new RaterInfo(u.Name, u.AvatarFileId, u.NafathVerified), ct);

        // Avatar files are loaded in one batch query (same dictionary pattern as deals/raters),
        // never one query per review.
        var avatarFileIds = raters.Values.Where(v => v.AvatarFileId is not null)
            .Select(v => v.AvatarFileId!.Value).Distinct().ToList();
        var avatarFiles = avatarFileIds.Count == 0
            ? new Dictionary<long, StoredFile>()
            : await db.Files.AsNoTracking().Where(f => avatarFileIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, ct);

        IReadOnlyList<SellerReviewDto> items = ratings.Select(x =>
        {
            var rater = raters.GetValueOrDefault(x.RaterId);
            string? avatarUrl = null;
            if (rater?.AvatarFileId is { } avatarId && avatarFiles.TryGetValue(avatarId, out var file))
                avatarUrl = files.Resolve(file);
            return new SellerReviewDto(
                x.Id, x.DealId, deals.GetValueOrDefault(x.DealId, string.Empty), x.Stars, x.Comment,
                rater?.Name ?? string.Empty, avatarUrl, rater?.NafathVerified ?? false, x.CreatedAt);
        }).ToList();

        return new PagedResult<SellerReviewDto>(items, page, pageSize, total);
    }

    private sealed record RaterInfo(string Name, long? AvatarFileId, bool NafathVerified);
}
