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

    /// <summary>
    /// Guest home: wallet state (always not activated), support WhatsApp and
    /// public FAQs. No login required — a guest has no user, deals or
    /// balances. The action-level [AllowAnonymous] bypasses the class-level
    /// [Authorize]; no separate controller, so the route stays exactly
    /// /api/v1/home/guest (BaseApiController routes by [controller], so a
    /// GuestHomeController would land on /api/v1/guesthome instead).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("guest")]
    public async Task<IActionResult> Guest(CancellationToken ct)
        => Respond(await Sender.Send(new GetGuestHomeQuery(), ct));
}
