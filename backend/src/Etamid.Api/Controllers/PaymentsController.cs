using System.Text;
using Etamid.Application.Deals.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

/// <summary>Called by the payment provider, not by users: authenticated only by the HMAC signature of the raw body.</summary>
[AllowAnonymous]
public sealed class PaymentsController : BaseApiController
{
    [HttpPost("webhook")]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // The signature covers the exact bytes sent, so the body is read raw and never re-serialized.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Signature"].FirstOrDefault();

        return Respond(await Sender.Send(new ProcessPaymentWebhookCommand(body, signature), ct), "WEBHOOK_OK");
    }
}
