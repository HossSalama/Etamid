using Etamid.Application.CompanyDeals;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>
/// Company deals: a separate path from individual deals (wizard draft,
/// counterparty acceptance, per-installment escrow, penalties). Money-moving
/// calls (fund) need an <c>X-Idempotency-Key</c> header so a retry never
/// charges twice.
/// </summary>
[Route("api/v1/company-deals")]
public sealed class CompanyDealsController : BaseApiController
{
    private string IdempotencyKey
        => Request.Headers.TryGetValue("X-Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key.ToString())
            ? key.ToString().Trim()
            : Request.Headers.TryGetValue("Idempotency-Key", out var alt) ? alt.ToString().Trim() : string.Empty;

    [HttpPost]
    public async Task<IActionResult> Create(CreateCompanyDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "COMPANY_DEAL_CREATED", StatusCodes.Status201Created);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetCompanyDealsQuery(status, page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct)
        => Respond(await Sender.Send(new GetCompanyDealQuery(id), ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Edit(long id, EditCompanyDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "COMPANY_DEAL_UPDATED");

    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct)
        => Respond(await Sender.Send(new SubmitCompanyDealCommand(id), ct), "COMPANY_DEAL_SUBMITTED");

    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, AcceptCompanyDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "COMPANY_DEAL_ACCEPTED");

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, RejectCompanyDealCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "COMPANY_DEAL_REJECTED");

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
        => Respond(await Sender.Send(new CancelCompanyDealCommand(id), ct), "COMPANY_DEAL_CANCELLED");

    [HttpPost("{id:long}/installments/{installmentId:long}/request-approval")]
    public async Task<IActionResult> RequestApproval(long id, long installmentId, CancellationToken ct)
        => Respond(await Sender.Send(new RequestInstallmentApprovalCommand(id, installmentId), ct),
            "COMPANY_INSTALLMENT_APPROVAL_REQUESTED");

    [HttpPost("{id:long}/installments/{installmentId:long}/approve")]
    public async Task<IActionResult> ApproveInstallment(long id, long installmentId, CancellationToken ct)
        => Respond(await Sender.Send(new ApproveInstallmentCommand(id, installmentId), ct),
            "COMPANY_INSTALLMENT_APPROVED");

    [HttpPost("{id:long}/installments/{installmentId:long}/reject")]
    public async Task<IActionResult> RejectInstallment(long id, long installmentId, RejectInstallmentCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { DealId = id, InstallmentId = installmentId }, ct),
            "COMPANY_INSTALLMENT_REJECTED");

    [HttpPost("{id:long}/installments/{installmentId:long}/fund")]
    public async Task<IActionResult> FundInstallment(long id, long installmentId, CancellationToken ct)
        => Respond(await Sender.Send(new FundInstallmentCommand(id, installmentId, IdempotencyKey), ct),
            "COMPANY_INSTALLMENT_FUNDING_STARTED");
}
