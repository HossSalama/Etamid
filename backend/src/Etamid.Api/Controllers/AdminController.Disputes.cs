using Etamid.Application.Disputes;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed partial class AdminController
{
    // ---------- Disputes (operator only, via the disputes.operators AppSetting) ----------

    [HttpGet("disputes")]
    public async Task<IActionResult> ListDisputes(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListDisputesQuery(status, page, pageSize), ct));

    [HttpPost("disputes/{disputeId:long}/request-info")]
    public async Task<IActionResult> RequestDisputeInfo(long disputeId, RequestDisputeInfoCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DisputeId = disputeId }, ct), "DISPUTE_INFO_REQUESTED");

    [HttpPost("disputes/{disputeId:long}/decide")]
    public async Task<IActionResult> DecideDispute(long disputeId, DecideDisputeCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DisputeId = disputeId }, ct), "DISPUTE_DECIDED");
}
