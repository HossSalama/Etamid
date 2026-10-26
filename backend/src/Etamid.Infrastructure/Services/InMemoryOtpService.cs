using System.Collections.Concurrent;
using Etamid.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Etamid.Infrastructure.Services;

public sealed class InMemoryOtpService(
    IEmailService emailService,
    IHostEnvironment env,
    ILogger<InMemoryOtpService> logger) : IOtpService
{
    private static readonly ConcurrentDictionary<string, (string Code, DateTime ExpiresAt)> OtpStore = new();

    public async Task<string> GenerateAndSendOtpAsync(
        string phoneNormalized,
        string email,
        string recipientName,
        CancellationToken cancellationToken = default)
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        OtpStore[phoneNormalized] = (code, expiresAt);
        logger.LogInformation("Generated OTP for {Phone} ({Email}): {Otp}", phoneNormalized, email, code);

        await emailService.SendOtpEmailAsync(email, recipientName, code, cancellationToken);
        return code;
    }

    public bool ValidateOtp(string phoneNormalized, string code)
    {
        if (string.IsNullOrWhiteSpace(phoneNormalized) || string.IsNullOrWhiteSpace(code))
            return false;

        var trimmedCode = code.Trim();

        // In non-production environments, allow developer test bypass 123456
        if (!env.IsProduction() && trimmedCode == "123456")
            return true;

        if (OtpStore.TryGetValue(phoneNormalized, out var entry))
        {
            if (entry.ExpiresAt >= DateTime.UtcNow && entry.Code == trimmedCode)
            {
                OtpStore.TryRemove(phoneNormalized, out _);
                return true;
            }
        }

        return false;
    }
}
