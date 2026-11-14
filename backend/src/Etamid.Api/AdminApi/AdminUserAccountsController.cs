using Etamid.Application.AdminAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.AdminApi;

/// <summary>
/// App users (companies and individuals) for the dashboard. Shares the admin/api/users prefix with the counts endpoint,
/// which is a different controller and is not changed. List/detail: admin.users.view. Delete: admin.users.manage.
/// </summary>
[Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
[Route("admin/api/users")]
public sealed class AdminUserAccountsController : AdminApiController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListAdminUsersQuery(type, status, search, page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Respond(await Sender.Send(new GetAdminUserQuery(id), ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => Respond(await Sender.Send(new DeleteAdminUserCommand(id), ct), "ADMIN_USER_DELETED");
}
