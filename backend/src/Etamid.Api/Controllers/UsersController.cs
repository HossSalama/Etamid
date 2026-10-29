using Etamid.Application.Identity.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>Seller preview before creating a deal (exact phone/code lookup only).</summary>
[Authorize]
public sealed class UsersController : BaseApiController
{
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string? q, CancellationToken ct)
        => Respond(await Sender.Send(new LookupSellerQuery(q ?? string.Empty), ct));
}
