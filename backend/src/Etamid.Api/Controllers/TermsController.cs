using Etamid.Application.Identity.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

[AllowAnonymous]
public sealed class TermsController : BaseApiController
{
    /// <summary>type = platform | deal | company</summary>
    [HttpGet("{type}")]
    public async Task<IActionResult> Get(string type, CancellationToken ct)
        => Respond(await Sender.Send(new GetTermsQuery(type), ct));
}
