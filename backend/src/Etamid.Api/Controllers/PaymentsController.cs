using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Commands;
using Etamid.Domain.Money;
using Etamid.Infrastructure.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Api.Controllers;

/// <summary>Called by the payment provider, not by users: authenticated only by the HMAC signature of the raw body.</summary>
public sealed class PaymentsController(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IConfiguration configuration,
    IWebHostEnvironment environment) : BaseApiController
{
    [HttpPost("webhook")]
    [AllowAnonymous]
    [RequestSizeLimit(65_536)]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // The signature covers the exact bytes sent, so the body is read raw and never re-serialized.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Signature"].FirstOrDefault();

        return Respond(await Sender.Send(new ProcessPaymentWebhookCommand(body, signature), ct), "WEBHOOK_OK");
    }

    /// <summary>Completes a Fake-gateway payment for local/integration happy-path testing. Never available in Production.</summary>
    [Authorize]
    [HttpPost("dev/deals/{dealId:long}/complete")]
    public async Task<IActionResult> CompleteFakePayment(long dealId, CancellationToken ct)
    {
        if (!(environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            || !string.Equals(configuration["Payments:Provider"] ?? "Fake", "Fake", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        if (currentUser.UserId is not { } userId) return Unauthorized();

        var payment = await db.Payments.AsNoTracking()
            .Where(p => p.DealId == dealId && p.UserId == userId && p.Provider == "Fake"
                        && (p.Status == PaymentStatus.Initiated || p.Status == PaymentStatus.RequiresAction))
            .OrderByDescending(p => p.Id)
            .Select(p => new { p.ProviderPaymentId, p.Amount, p.Currency })
            .FirstOrDefaultAsync(ct);

        if (payment is null || string.IsNullOrWhiteSpace(payment.ProviderPaymentId)) return NotFound();

        var secret = configuration["Payments:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Set Payments:WebhookSecret (at least 32 characters) in the Development environment to simulate a payment.");

        var body = JsonSerializer.Serialize(new
        {
            eventId = $"dev_{Guid.NewGuid():N}",
            type = "payment.succeeded",
            providerPaymentId = payment.ProviderPaymentId,
            amount = payment.Amount,
            currency = payment.Currency
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var signature = HmacWebhookSignatureVerifier.Sign(secret, body);
        return Respond(await Sender.Send(new ProcessPaymentWebhookCommand(body, signature), ct), "WEBHOOK_OK");
    }
}
