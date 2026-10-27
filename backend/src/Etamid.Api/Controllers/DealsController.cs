using Etamid.Application.Deals.Commands;
using Etamid.Application.Deals.Queries;
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

    [HttpPost("{id:long}/fund")]
    public async Task<IActionResult> Fund(long id, CancellationToken ct)
        => Respond(await Sender.Send(new FundDealCommand(id, IdempotencyKey), ct), "DEAL_FUNDING_STARTED");

    [HttpPost("{id:long}/deliver")]
    public async Task<IActionResult> Deliver(long id, CancellationToken ct)
        => Respond(await Sender.Send(new DeliverDealCommand(id), ct), "DEAL_DELIVERED");

    [HttpPost("{id:long}/release")]
    public async Task<IActionResult> Release(long id, CancellationToken ct)
        => Respond(await Sender.Send(new ReleaseEscrowCommand(id, IdempotencyKey), ct), "DEAL_RELEASED");

    [HttpPost("{id:long}/dispute")]
    public async Task<IActionResult> Dispute(long id, OpenDisputeCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id }, ct), "DEAL_DISPUTE_OPENED", StatusCodes.Status201Created);
}
