using Etamid.Application.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

[AllowAnonymous]
public sealed class ConfigController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Respond(await Sender.Send(new GetAppConfigQuery(), ct));
}

[AllowAnonymous]
public sealed class LookupsController : BaseApiController
{
    /// <summary>reject-reasons | dispute-reasons | company-reject-reasons | duration-units | payment-methods | store-categories | banks | shipping-companies</summary>
    [HttpGet("{type}")]
    public async Task<IActionResult> Get(string type, CancellationToken ct)
        => Respond(await Sender.Send(new GetLookupsQuery(type), ct));
}

[AllowAnonymous]
public sealed class SupportController : BaseApiController
{
    [HttpGet("faqs")]
    public async Task<IActionResult> Faqs(CancellationToken ct)
        => Respond(await Sender.Send(new GetFaqsQuery(), ct));
}
