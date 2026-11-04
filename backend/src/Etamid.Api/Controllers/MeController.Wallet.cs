using Etamid.Application.Money;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed partial class MeController
{
    // ---------- Wallet ----------

    [HttpGet("wallet")]
    public async Task<IActionResult> GetWallet(CancellationToken ct)
        => Respond(await Sender.Send(new GetMyWalletQuery(), ct));

    [HttpPost("wallet/activate")]
    public async Task<IActionResult> ActivateWallet(CancellationToken ct)
        => Respond(await Sender.Send(new ActivateWalletCommand(), ct), "WALLET_ACTIVATED");

    [HttpGet("wallet/transactions")]
    public async Task<IActionResult> GetWalletTransactions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetWalletTransactionsQuery(page, pageSize), ct));

    [HttpGet("wallet/withdrawals")]
    public async Task<IActionResult> GetMyWithdrawals(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetMyWithdrawalsQuery(page, pageSize), ct));

    [HttpPost("wallet/withdrawals")]
    public async Task<IActionResult> RequestWithdrawal(RequestWithdrawalCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "WALLET_WITHDRAWAL_REQUESTED", StatusCodes.Status201Created);
}
