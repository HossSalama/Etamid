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

    [HttpGet("disputes/{disputeId:long}")]
    public async Task<IActionResult> GetDisputeDetail(long disputeId, CancellationToken ct = default)
        => Respond(await Sender.Send(new GetDisputeDetailQuery(disputeId), ct));

    [HttpPost("disputes/{disputeId:long}/request-info")]
    public async Task<IActionResult> RequestDisputeInfo(long disputeId, RequestDisputeInfoCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DisputeId = disputeId }, ct), "DISPUTE_INFO_REQUESTED");

    // The admin never decides a dispute (16 Nov 2026): this executes what the
    // buyer and the seller agreed between themselves, through the admin.
    [HttpPost("disputes/{disputeId:long}/execute-agreement")]
    public async Task<IActionResult> ExecuteAgreement(long disputeId, ExecuteAgreementCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DisputeId = disputeId }, ct), "DISPUTE_AGREEMENT_EXECUTED");

    // The dispute could not be resolved: the admin sends its documents to the
    // buyer so the buyer can take the case to court. Moves no money.
    [HttpPost("disputes/{disputeId:long}/send-documents")]
    public async Task<IActionResult> SendDisputeDocuments(long disputeId, SendDisputeDocumentsCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DisputeId = disputeId }, ct), "DISPUTE_DOCUMENTS_SENT");

    // The admin mediates in the thread: a message here is visible to both parties.
    [HttpPost("disputes/{disputeId:long}/messages")]
    public async Task<IActionResult> PostDisputeMessage(long disputeId, PostAdminDisputeMessageCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DisputeId = disputeId }, ct), "DISPUTE_MESSAGE_SENT", StatusCodes.Status201Created);
}
