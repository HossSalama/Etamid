using Etamid.Application.Support;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed partial class AdminController
{
    // ---------- Support tickets (operator only, admin.support.manage) ----------

    [HttpGet("support/tickets")]
    public async Task<IActionResult> ListSupportTickets(
        [FromQuery] string? status, [FromQuery] long? assignedAdminId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new ListSupportTicketsQuery(status, assignedAdminId, page, pageSize), ct));

    [HttpGet("support/tickets/{ticketId:long}")]
    public async Task<IActionResult> GetSupportTicket(long ticketId, CancellationToken ct = default)
        => Respond(await Sender.Send(new GetAdminSupportTicketDetailQuery(ticketId), ct));

    [HttpPost("support/tickets/{ticketId:long}/assign")]
    public async Task<IActionResult> AssignSupportTicket(
        long ticketId, AssignSupportTicketCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { TicketId = ticketId }, ct), "SUPPORT_TICKET_ASSIGNED");

    [HttpPost("support/tickets/{ticketId:long}/messages")]
    public async Task<IActionResult> PostAdminSupportTicketMessage(
        long ticketId, PostAdminSupportTicketMessageCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { TicketId = ticketId }, ct),
            "SUPPORT_TICKET_MESSAGE_SENT", StatusCodes.Status201Created);

    [HttpPost("support/tickets/{ticketId:long}/close")]
    public async Task<IActionResult> CloseSupportTicket(long ticketId, CancellationToken ct)
        => Respond(await Sender.Send(new CloseAdminSupportTicketCommand(ticketId), ct), "SUPPORT_TICKET_CLOSED");
}
