using Etamid.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>In-app notifications (Layer 1). Every action is scoped to the caller's own notifications.</summary>
[Authorize]
public sealed class NotificationsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => RespondPaged(await Sender.Send(new GetNotificationsQuery(page, pageSize), ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
        => Respond(await Sender.Send(new GetUnreadCountQuery(), ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
        => Respond(await Sender.Send(new MarkNotificationReadCommand(id), ct), "NOTIFICATION_READ");

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => Respond(await Sender.Send(new MarkAllNotificationsReadCommand(), ct), "NOTIFICATIONS_READ_ALL");
}
