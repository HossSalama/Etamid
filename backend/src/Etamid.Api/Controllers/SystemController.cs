using Etamid.Application.Platform.Queries.Ping;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed class SystemController : BaseApiController
{
    /// <summary>Lightweight check used by the mobile team and by integration tests.</summary>
    [HttpGet("ping")]
    public async Task<IActionResult> Ping(CancellationToken ct)
        => Respond(await Sender.Send(new PingQuery(), ct), "PING_OK");
}
