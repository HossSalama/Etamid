using System.Security.Cryptography;
using System.Text;
using Etamid.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Etamid.Infrastructure.Payments;

/// <summary>
/// HMAC-SHA256 over the raw request body with the shared secret <c>Payments:WebhookSecret</c>, hex encoded
/// (an optional "sha256=" prefix is accepted). Fails closed: with no secret configured every webhook is rejected.
/// </summary>
public sealed class HmacWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private const int MinSecretLength = 32;
    private readonly byte[]? _secret;

    public HmacWebhookSignatureVerifier(IConfiguration configuration, ILogger<HmacWebhookSignatureVerifier> logger)
    {
        var secret = configuration["Payments:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < MinSecretLength)
        {
            logger.LogWarning("Payments:WebhookSecret is missing or shorter than {Min} characters: every payment webhook will be rejected.", MinSecretLength);
            return;
        }
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public bool IsValid(string rawBody, string? signature)
    {
        if (_secret is null || string.IsNullOrWhiteSpace(signature)) return false;

        var provided = signature.Trim();
        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) provided = provided["sha256=".Length..];

        var expected = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(rawBody));
        byte[] actual;
        try { actual = Convert.FromHexString(provided); }
        catch (FormatException) { return false; }

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>Signs a body the same way a provider would (used by tests and local tooling).</summary>
    public static string Sign(string secret, string rawBody)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
}
