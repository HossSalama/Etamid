using Etamid.Application.Config;
using Etamid.Application.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

[AllowAnonymous]
public sealed class ConfigController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Respond(await Sender.Send(new GetAppConfigQuery(), ct));
}

[AllowAnonymous]
public sealed class LookupsController : BaseApiController
{
    /// <summary>reject-reasons | dispute-reasons | company-reject-reasons | duration-units | payment-methods | store-categories | banks | shipping-companies | ticket-subjects</summary>
    [HttpGet("{type}")]
    public async Task<IActionResult> Get(string type, CancellationToken ct)
        => Respond(await Sender.Send(new GetLookupsQuery(type), ct));
}

[Authorize]
public sealed class SupportController : BaseApiController
{
    // ?search= filters question + answer in both languages; empty = all FAQs.
    [AllowAnonymous]
    [HttpGet("faqs")]
    public async Task<IActionResult> Faqs([FromQuery] string? search, CancellationToken ct)
        => Respond(await Sender.Send(new GetFaqsQuery(search), ct));

    // The contact form creates a ticket: subject (dropdown) + message (max 200 chars).
    [HttpPost("contact")]
    public async Task<IActionResult> Contact(CreateContactMessageCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "SUPPORT_CONTACT_SENT", StatusCodes.Status201Created);

    // The public website contact form: no login, stored as a guest message (not a ticket).
    [AllowAnonymous]
    [HttpPost("contact-public")]
    public async Task<IActionResult> ContactPublic(CreateGuestContactMessageCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "SUPPORT_CONTACT_SENT", StatusCodes.Status201Created);

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(CreateSupportTicketCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "SUPPORT_TICKET_CREATED", StatusCodes.Status201Created);

    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetMySupportTicketsQuery(status, page, pageSize), ct));

    [HttpGet("tickets/{ticketId:long}")]
    public async Task<IActionResult> GetTicket(long ticketId, CancellationToken ct = default)
        => Respond(await Sender.Send(new GetSupportTicketDetailQuery(ticketId), ct));

    [HttpPost("tickets/{ticketId:long}/messages")]
    public async Task<IActionResult> PostTicketMessage(
        long ticketId, PostSupportTicketMessageCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { TicketId = ticketId }, ct),
            "SUPPORT_TICKET_MESSAGE_SENT", StatusCodes.Status201Created);

    [HttpPost("tickets/{ticketId:long}/close")]
    public async Task<IActionResult> CloseTicket(long ticketId, CancellationToken ct)
        => Respond(await Sender.Send(new CloseSupportTicketCommand(ticketId), ct), "SUPPORT_TICKET_CLOSED");
}
