using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Commands;

// ======================================================================= Rate a deal
/// <summary>Buyer rates the seller of a released deal. One rating per deal, stars 1-5, optional comment (max 300).</summary>
public sealed record RateDealCommand(long DealId, int Stars, string? Comment) : IRequest<Result<RatingDto>>;

public sealed class RateDealValidator : AbstractValidator<RateDealCommand>
{
    public RateDealValidator()
    {
        RuleFor(x => x.Stars).InclusiveBetween(1, 5).WithErrorCode("INVALID_VALUE");
        RuleFor(x => x.Comment).MaximumLength(300).WithErrorCode("INVALID_VALUE").When(x => x.Comment is not null);
    }
}

public sealed class RateDealHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<RateDealCommand, Result<RatingDto>>
{
    public async Task<Result<RatingDto>> Handle(RateDealCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == r.DealId, ct);

        // A stranger gets the same 404 as a missing deal, so ids cannot be probed.
        if (deal is null || !deal.IsParticipant(userId)) return DealErrors.NotFound;
        if (deal.BuyerId != userId) return DealErrors.NotAllowed;
        if (deal.Status != DealStatus.Released) return DealErrors.InvalidState;
        if (await db.Ratings.AnyAsync(x => x.DealId == deal.Id, ct)) return DealErrors.RatingAlreadyExists;

        var seller = await db.Users.FirstOrDefaultAsync(u => u.Id == deal.SellerId, ct);
        if (seller is null) return DealErrors.NotFound;

        var comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim();
        var rating = new Rating
        {
            DealId = deal.Id,
            RaterId = userId,
            SellerId = deal.SellerId,
            Stars = (byte)r.Stars,
            Comment = comment,
            IsVisible = true
        };
        db.Ratings.Add(rating);

        // Keep the seller's store aggregates in sync immediately: the lookup
        // preview and the store page read RatingAvg/RatingCount, and nothing
        // else updates them, so a rating that skips this step would stay invisible.
        var store = await db.StoreProfiles.FirstOrDefaultAsync(s => s.UserId == deal.SellerId, ct);
        if (store is null)
        {
            store = new StoreProfile { UserId = deal.SellerId, StoreName = seller.Name };
            db.StoreProfiles.Add(store);
        }
        store.RatingAvg = decimal.Round((store.RatingAvg * store.RatingCount + r.Stars) / (store.RatingCount + 1), 2);
        store.RatingCount += 1;

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return new RatingDto(rating.Id, rating.DealId, rating.SellerId, rating.Stars, rating.Comment, rating.CreatedAt);
    }
}
