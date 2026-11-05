using Etamid.Application.Deals.Commands;
using Etamid.Application.Deals.Queries;
using Etamid.Application.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>
/// Deal and escrow cycle. Money-moving calls (fund, release) need an <c>X-Idempotency-Key</c> header so a retry
/// never charges or releases twice.
/// </summary>
[Authorize]
public sealed class DealsController : BaseApiController
{
    private string IdempotencyKey
        => Request.Headers.TryGetValue("X-Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key.ToString())
            ? key.ToString().Trim()
            : Request.Headers.TryGetValue("Idempotency-Key", out var alt) ? alt.ToString().Trim() : string.Empty;

    [HttpPost]
    public async Task<IActionResult> Create(CreateDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "DEAL_CREATED", StatusCodes.Status201Created);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? role, [FromQuery] string? tab, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetMyDealsQuery(role, tab, page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
        => Respond(await Sender.Send(new GetDealQuery(id), ct));

    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, AcceptDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DEAL_ACCEPTED");

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, RejectDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DEAL_REJECTED");

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
        => Respond(await Sender.Send(new CancelDealCommand(id), ct), "DEAL_CANCELLED");

    [HttpPost("{id:long}/edits")]
    public async Task<IActionResult> ProposeEdit(long id, ProposeDealEditCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DEAL_EDIT_PROPOSED", StatusCodes.Status201Created);

    [HttpPost("{id:long}/edits/{editId:long}/approve")]
    public async Task<IActionResult> ApproveEdit(long id, long editId, CancellationToken ct)
        => Respond(await Sender.Send(new RespondDealEditCommand(id, editId, Approve: true), ct), "DEAL_EDIT_APPROVED");

    [HttpPost("{id:long}/edits/{editId:long}/reject")]
    public async Task<IActionResult> RejectEdit(long id, long editId, CancellationToken ct)
        => Respond(await Sender.Send(new RespondDealEditCommand(id, editId, Approve: false), ct), "DEAL_EDIT_REJECTED");

    [HttpPost("{id:long}/edits/{editId:long}/cancel")]
    public async Task<IActionResult> CancelEdit(long id, long editId, CancellationToken ct)
        => Respond(await Sender.Send(new CancelDealEditCommand(id, editId), ct), "DEAL_EDIT_CANCELLED");

    [HttpPost("{id:long}/fund")]
    public async Task<IActionResult> Fund(long id, CancellationToken ct)
        => Respond(await Sender.Send(new FundDealCommand(id, IdempotencyKey), ct), "DEAL_FUNDING_STARTED");

    [HttpPost("{id:long}/deliver")]
    public async Task<IActionResult> Deliver(long id, DeliverDealCommand? command, CancellationToken ct)
        // The body is optional: older clients post an empty body, newer ones send { trackingNumber }.
        => Respond(await Sender.Send(new DeliverDealCommand(id, command?.TrackingNumber), ct), "DEAL_DELIVERED");

    [HttpGet("{id:long}/tracking")]
    public async Task<IActionResult> Tracking(long id, CancellationToken ct)
        => Respond(await Sender.Send(new GetDealTrackingQuery(id), ct));

    [HttpPost("{id:long}/release")]
    public async Task<IActionResult> Release(long id, CancellationToken ct)
        => Respond(await Sender.Send(new ReleaseEscrowCommand(id, IdempotencyKey), ct), "DEAL_RELEASED");

    [HttpPost("{id:long}/dispute")]
    public async Task<IActionResult> Dispute(long id, OpenDisputeCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DEAL_DISPUTE_OPENED", StatusCodes.Status201Created);

    [HttpGet("{id:long}/dispute")]
    public async Task<IActionResult> GetDispute(long id, CancellationToken ct)
        => Respond(await Sender.Send(new GetDisputeQuery(id), ct));

    [HttpPost("{id:long}/dispute/messages")]
    public async Task<IActionResult> PostDisputeMessage(long id, PostDisputeMessageCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DISPUTE_MESSAGE_SENT", StatusCodes.Status201Created);

    [HttpPost("{id:long}/dispute/attachments")]
    public async Task<IActionResult> AddDisputeAttachment(long id, AddDisputeAttachmentCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DISPUTE_ATTACHMENT_ADDED", StatusCodes.Status201Created);

    [HttpPost("{id:long}/dispute/settlement/propose")]
    public async Task<IActionResult> ProposeSettlement(long id, ProposeSettlementCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DISPUTE_SETTLEMENT_PROPOSED", StatusCodes.Status201Created);

    [HttpPost("{id:long}/dispute/settlement/{proposalId:long}/accept")]
    public async Task<IActionResult> AcceptSettlement(long id, long proposalId, CancellationToken ct)
        => Respond(await Sender.Send(new AcceptSettlementCommand(id, proposalId), ct), "DISPUTE_SETTLEMENT_ACCEPTED");

    [HttpPost("{id:long}/dispute/settlement/{proposalId:long}/reject")]
    public async Task<IActionResult> RejectSettlement(long id, long proposalId, CancellationToken ct)
        => Respond(await Sender.Send(new RejectSettlementCommand(id, proposalId), ct), "DISPUTE_SETTLEMENT_REJECTED");

    [HttpPost("{id:long}/rating")]
    public async Task<IActionResult> Rate(long id, RateDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "RATING_CREATED", StatusCodes.Status201Created);
}
