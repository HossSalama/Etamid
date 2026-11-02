using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Queries;

/// <summary>One visible review on a seller's page: stars, comment, the deal it came from, and the rater's display name only.</summary>
public sealed record SellerReviewDto(
    long Id, long DealId, string DealTitle, int Stars, string? Comment, string RaterName, DateTime CreatedAt);

public sealed record GetSellerReviewsQuery(long SellerId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<SellerReviewDto>>>;

public sealed class GetSellerReviewsHandler(IApplicationDbContext db, ICurrentUser currentUser)
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
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        IReadOnlyList<SellerReviewDto> items = ratings.Select(x => new SellerReviewDto(
            x.Id, x.DealId, deals.GetValueOrDefault(x.DealId, string.Empty), x.Stars, x.Comment,
            raters.GetValueOrDefault(x.RaterId, string.Empty), x.CreatedAt)).ToList();

        return new PagedResult<SellerReviewDto>(items, page, pageSize, total);
    }
}
