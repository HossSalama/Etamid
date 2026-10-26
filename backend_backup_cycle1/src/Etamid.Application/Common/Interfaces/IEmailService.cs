namespace Etamid.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string recipientName, string otpCode, CancellationToken cancellationToken = default);
}
