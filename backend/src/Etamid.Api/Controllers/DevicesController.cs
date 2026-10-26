using Etamid.Application.Identity.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

[Authorize]
public sealed class DevicesController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "DEVICE_REGISTERED");

    [HttpDelete("{deviceToken}")]
    public async Task<IActionResult> Unregister(string deviceToken, CancellationToken ct)
        => Respond(await Sender.Send(new UnregisterDeviceCommand(deviceToken), ct), "DEVICE_REMOVED");
}
