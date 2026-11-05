using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Disputes;

// ---------- GET /deals/{id}/dispute ----------

public sealed record GetDisputeQuery(long DealId) : IRequest<Result<DisputeDetailDto>>;

public sealed class GetDisputeHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<GetDisputeQuery, Result<DisputeDetailDto>>
{
    public async Task<Result<DisputeDetailDto>> Handle(GetDisputeQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var deal = await db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == r.DealId, ct);
        if (deal is null) return DealErrors.NotFound;

        var isOperator = await DisputeOperatorPolicy.IsOperatorAsync(db, userId, ct);
        if (!deal.IsParticipant(userId) && !isOperator) return DealErrors.NotFound;

        var dispute = await db.Disputes
            .Where(d => d.DealId == deal.Id).OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (dispute is null) return DisputeErrors.NotFound;

        return await DisputeMapper.DetailAsync(
            db, dispute, deal.BuyerId, deal.SellerId, deal.Title, deal.Amount, deal.Currency,
            userId, isOperator, ct, files);
    }
}

// ---------- GET /me/disputes ----------

public sealed record GetMyDisputesQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<DisputeSummaryDto>>>;

public sealed class GetMyDisputesHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMyDisputesQuery, Result<PagedResult<DisputeSummaryDto>>>
{
    public async Task<Result<PagedResult<DisputeSummaryDto>>> Handle(GetMyDisputesQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        DisputeStatus? status = null;
        if (!string.IsNullOrWhiteSpace(r.Status))
        {
            if (!Enum.TryParse<DisputeStatus>(r.Status, ignoreCase: true, out var parsed))
                return Error.Validation("INVALID_VALUE", "status");
            status = parsed;
        }

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.Disputes.AsNoTracking()
            .Where(d => d.OpenedById == userId || d.AgainstUserId == userId);
        if (status is { } s) query = query.Where(d => d.Status == s);

        var total = await query.CountAsync(ct);
        var disputes = await query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = new List<DisputeSummaryDto>();
        foreach (var dispute in disputes)
            items.Add(await DisputeMapper.SummaryAsync(db, dispute, ct));

        return new PagedResult<DisputeSummaryDto>(items, page, pageSize, total);
    }
}
