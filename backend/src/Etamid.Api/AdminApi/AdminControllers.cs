using Etamid.Application.AdminAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Etamid.Api.AdminApi;

/// <summary>Admin authentication. Anonymous steps are rate-limited per IP (admin-auth policy).</summary>
[Route("admin/api/auth")]
[EnableRateLimiting("admin-auth")]
public sealed class AdminAuthController : AdminApiController
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(AdminLoginCommand command, CancellationToken ct)
    {
        var result = await Sender.Send(command, ct);
        return Respond(result, result.IsSuccess && result.Value.TwoFactorRequired ? "ADMIN_2FA_REQUIRED" : "ADMIN_LOGGED_IN");
    }

    [AllowAnonymous]
    [HttpPost("2fa/verify")]
    public async Task<IActionResult> VerifyTwoFactor(AdminVerifyTwoFactorCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ADMIN_LOGGED_IN");

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(AdminRefreshCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ADMIN_TOKEN_REFRESHED");

    [Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AdminLogoutCommand? command, CancellationToken ct)
        => Respond(await Sender.Send(command ?? new AdminLogoutCommand(null), ct), "ADMIN_LOGGED_OUT");

    [Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
        => Respond(await Sender.Send(new AdminLogoutAllCommand(), ct), "ADMIN_LOGGED_OUT");

    [Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => Respond(await Sender.Send(new AdminMeQuery(), ct));

    [Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor(CancellationToken ct)
        => Respond(await Sender.Send(new AdminTwoFactorSetupCommand(), ct), "ADMIN_2FA_SETUP_STARTED");

    [Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(AdminTwoFactorEnableCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ADMIN_2FA_ENABLED");

    [Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(AdminTwoFactorDisableCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ADMIN_2FA_DISABLED");
}

[Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
[Route("admin/api/admins")]
public sealed class AdminAccountsController : AdminApiController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListAdminsQuery(status, page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Respond(await Sender.Send(new GetAdminQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateAdminCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ADMIN_CREATED", StatusCodes.Status201Created);

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateAdminCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "ADMIN_UPDATED");

    [HttpPost("{id:long}/disable")]
    public async Task<IActionResult> Disable(long id, CancellationToken ct)
        => Respond(await Sender.Send(new DisableAdminCommand(id), ct), "ADMIN_DISABLED");

    [HttpPost("{id:long}/enable")]
    public async Task<IActionResult> Enable(long id, CancellationToken ct)
        => Respond(await Sender.Send(new EnableAdminCommand(id), ct), "ADMIN_ENABLED");

    [HttpPost("{id:long}/reset-2fa")]
    public async Task<IActionResult> ResetTwoFactor(long id, CancellationToken ct)
        => Respond(await Sender.Send(new ResetAdminTwoFactorCommand(id), ct), "ADMIN_2FA_RESET");

    [HttpPost("{id:long}/roles")]
    public async Task<IActionResult> SetRoles(long id, SetAdminRolesCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { AdminId = id }, ct), "ADMIN_ROLES_ASSIGNED");
}

[Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
[Route("admin/api/roles")]
public sealed class AdminRolesController : AdminApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Respond(await Sender.Send(new ListAdminRolesQuery(), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Respond(await Sender.Send(new GetAdminRoleQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateAdminRoleCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ADMIN_ROLE_CREATED", StatusCodes.Status201Created);

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateAdminRoleCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "ADMIN_ROLE_UPDATED");

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => Respond(await Sender.Send(new DeleteAdminRoleCommand(id), ct), "ADMIN_ROLE_DELETED");

    [HttpPost("{id:long}/permissions")]
    public async Task<IActionResult> SetPermissions(long id, SetAdminRolePermissionsCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { RoleId = id }, ct), "ADMIN_ROLE_PERMISSIONS_SET");
}

[Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
[Route("admin/api/permissions")]
public sealed class AdminPermissionsController : AdminApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Respond(await Sender.Send(new ListAdminPermissionsQuery(), ct));
}

[Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
[Route("admin/api/audit-logs")]
public sealed class AdminAuditLogsController : AdminApiController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? action, [FromQuery] string? entityType, [FromQuery] long? actorId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListAuditLogsQuery(action, entityType, actorId, page, pageSize), ct));
}

[Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
[Route("admin/api/users")]
public sealed class AdminAppUsersController : AdminApiController
{
    [HttpGet("counts")]
    public async Task<IActionResult> Counts(CancellationToken ct)
        => Respond(await Sender.Send(new GetAppUserCountsQuery(), ct));
}

[Authorize(AuthenticationSchemes = AdminAuthSchemes.Jwt)]
[Route("admin/api/deals")]
public sealed class AdminDealsController : AdminApiController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListAdminDealsQuery(status, page, pageSize), ct));
}
