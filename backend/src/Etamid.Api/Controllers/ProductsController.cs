using Etamid.Application.Identity.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>The shop feed: every seller's products for any logged-in user.
/// Like the store page, this requires login; guests are sent to login by the web app.</summary>
[Authorize]
public sealed class ProductsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Shop(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListShopProductsQuery(page, pageSize), ct));
}
