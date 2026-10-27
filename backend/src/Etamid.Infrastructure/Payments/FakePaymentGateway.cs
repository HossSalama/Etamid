using Etamid.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Etamid.Infrastructure.Payments;

/// <summary>
/// Development/test gateway: hands out a fake checkout link and never moves real money. Money is only
/// considered received when a correctly signed webhook says so (see HmacWebhookSignatureVerifier).
/// Refused in Production (see DependencyInjection).
/// </summary>
public sealed class FakePaymentGateway(IConfiguration configuration) : IPaymentGateway
{
    public string Name => "Fake";

    public Task<PaymentSession> CreateSessionAsync(
        Guid paymentPublicId, decimal amount, string currency, long dealId, CancellationToken cancellationToken)
    {
        var baseUrl = (configuration["Payments:CheckoutBaseUrl"] ?? "https://pay.etamid.local").TrimEnd('/');
        var session = new PaymentSession($"fake_{paymentPublicId:N}", $"{baseUrl}/checkout/{paymentPublicId:N}");
        return Task.FromResult(session);
    }
}
