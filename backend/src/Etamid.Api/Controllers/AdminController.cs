using Etamid.Application.Money;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>
/// Operator endpoints (no admin login exists yet — see WalletOperatorPolicy).
/// Withdrawal approval is a manual bank transfer confirmed here, never automated.
/// </summary>
[Authorize]
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
