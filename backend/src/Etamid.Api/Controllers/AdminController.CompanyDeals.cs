using Etamid.Application.CompanyDeals;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed partial class AdminController
{
    // ---------- Company-deal penalties (admin permissions) ----------

    [HttpGet("company-deals/penalties")]
    public async Task<IActionResult> ListCompanyPenalties(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListCompanyPenaltiesQuery(status, page, pageSize), ct));

    [HttpPost("company-deals/penalties/{penaltyId:long}/waive")]
    public async Task<IActionResult> WaiveCompanyPenalty(long penaltyId, WaiveCompanyPenaltyCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { PenaltyId = penaltyId }, ct), "COMPANY_PENALTY_WAIVED");
}
