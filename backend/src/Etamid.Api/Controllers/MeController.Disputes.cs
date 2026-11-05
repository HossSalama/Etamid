using Etamid.Application.Disputes;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed partial class MeController
{
    // ---------- Disputes ----------

    [HttpGet("disputes")]
    public async Task<IActionResult> GetMyDisputes(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetMyDisputesQuery(status, page, pageSize), ct));
}
