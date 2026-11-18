using Etamid.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>One call for the mobile home screen: user + wallet + active deals.</summary>
[Authorize]
public sealed class HomeController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Respond(await Sender.Send(new GetHomeQuery(), ct));
}
