using Etamid.Api.AdminApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Etamid.Application.Money;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>
/// Operator endpoints. Cycle 9 phase 2: gated by an AdminJwt identity plus a database-checked permission
/// (AdminAccessService). The app scheme is listed too so an app-user token is refused with 403, not 401.
/// Withdrawal approval is a manual bank transfer confirmed here, never automated.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme + "," + AdminAuthSchemes.Jwt)]
public sealed partial class AdminController : BaseApiController
{
    [HttpGet("withdrawals")]
    public async Task<IActionResult> ListWithdrawals(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListWithdrawalsQuery(status, page, pageSize), ct));

    [HttpPost("withdrawals/{id:long}/approve")]
    public async Task<IActionResult> ApproveWithdrawal(long id, CancellationToken ct)
        => Respond(await Sender.Send(new ApproveWithdrawalCommand(id), ct), "WALLET_WITHDRAWAL_APPROVED");

    [HttpPost("withdrawals/{id:long}/reject")]
    public async Task<IActionResult> RejectWithdrawal(long id, RejectWithdrawalCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "WALLET_WITHDRAWAL_REJECTED");

    [HttpPost("withdrawals/{id:long}/complete")]
    public async Task<IActionResult> CompleteWithdrawal(long id, CompleteWithdrawalCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "WALLET_WITHDRAWAL_COMPLETED");
}
