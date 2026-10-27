namespace Etamid.Application.Common.Interfaces;

/// <summary>A hosted checkout session created at the payment provider.</summary>
public sealed record PaymentSession(string ProviderPaymentId, string RedirectUrl);

/// <summary>The payment provider. Only the (signed) webhook can confirm that money arrived.</summary>
public interface IPaymentGateway
{
    string Name { get; }

    Task<PaymentSession> CreateSessionAsync(
        Guid paymentPublicId, decimal amount, string currency, long dealId, CancellationToken cancellationToken);
}

/// <summary>Verifies the signature a provider puts on its webhook calls (HMAC over the raw body).</summary>
public interface IWebhookSignatureVerifier
{
    bool IsValid(string rawBody, string? signature);
}
