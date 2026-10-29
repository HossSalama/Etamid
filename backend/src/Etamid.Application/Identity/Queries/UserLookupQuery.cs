using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Queries;

/// <summary>
/// Seller preview before creating a deal. This is an exact lookup by Saudi phone
/// or public user code — the same match CreateDeal uses — not a directory search:
/// partial queries return nothing, so accounts cannot be enumerated by guessing.
/// The caller is excluded (you cannot deal with yourself), and the response
/// deliberately contains no id, phone or email.
/// </summary>
public sealed record LookupSellerQuery(string Q) : IRequest<Result<CounterpartyDto>>;

public sealed class LookupSellerValidator : AbstractValidator<LookupSellerQuery>
{
    public LookupSellerValidator()
    {
        RuleFor(x => x.Q).NotEmpty().WithErrorCode("REQUIRED").MaximumLength(50).WithErrorCode("TOO_LONG");
    }
}

public sealed class LookupSellerHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<LookupSellerQuery, Result<CounterpartyDto>>
{
    public async Task<Result<CounterpartyDto>> Handle(LookupSellerQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var seller = await UserLookup.FindByPhoneOrCodeAsync(db, r.Q, ct);
        if (seller is null || seller.Id == userId) return DealErrors.SellerNotFound;

        var store = await db.StoreProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == seller.Id, ct);

        string? avatarUrl = null;
        if (seller.AvatarFileId is { } avatarId)
            avatarUrl = files.Resolve(await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == avatarId, ct));

        return new CounterpartyDto(
            seller.Name, seller.Code, store?.StoreName, avatarUrl,
            seller.EmailVerifiedAt is not null, seller.NafathVerified,
            store?.RatingAvg ?? 0m, store?.SuccessfulDeals ?? 0);
    }
}
