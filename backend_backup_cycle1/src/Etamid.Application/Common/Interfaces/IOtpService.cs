namespace Etamid.Application.Common.Interfaces;

public interface IOtpService
{
    Task<string> GenerateAndSendOtpAsync(string phoneNormalized, string email, string recipientName, CancellationToken cancellationToken = default);
    bool ValidateOtp(string phoneNormalized, string code);
}
