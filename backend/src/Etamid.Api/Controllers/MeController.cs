using Etamid.Application.Identity.Commands;
using Etamid.Application.Identity.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

[Authorize]
public sealed partial class MeController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Respond(await Sender.Send(new GetMyProfileQuery(), ct));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateProfileCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PROFILE_UPDATED");

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PASSWORD_CHANGED");

    /// <summary>Delete my account (soft delete). Body: { "password": "..." }.</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete(DeleteAccountCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ACCOUNT_DELETED");

    [HttpPut("language")]
    public async Task<IActionResult> ChangeLanguage(ChangeLanguageCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "LANGUAGE_UPDATED");
}
