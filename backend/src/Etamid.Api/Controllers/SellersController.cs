using Etamid.Application.Deals.Queries;
using Etamid.Application.Identity.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>Seller store pages: the store itself plus visible reviews only, no private data and never the seller's deals.</summary>
[Authorize]
public sealed class SellersController : BaseApiController
{
    [HttpGet("{sellerId:long}/store")]
    public async Task<IActionResult> Store(long sellerId, CancellationToken ct)
        => Respond(await Sender.Send(new GetSellerStoreQuery(sellerId), ct));

    [HttpGet("{sellerId:long}/products")]
    public async Task<IActionResult> Products(long sellerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetSellerProductsQuery(sellerId, page, pageSize), ct));

    [HttpGet("{sellerId:long}/reviews")]
    public async Task<IActionResult> Reviews(long sellerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetSellerReviewsQuery(sellerId, page, pageSize), ct));
}
