using Etamid.Application.Deals.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>Public-facing seller pages: visible reviews only, no private rater data.</summary>
[Authorize]
public sealed class SellersController : BaseApiController
{
    [HttpGet("{sellerId:long}/reviews")]
    public async Task<IActionResult> Reviews(long sellerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetSellerReviewsQuery(sellerId, page, pageSize), ct));
}
