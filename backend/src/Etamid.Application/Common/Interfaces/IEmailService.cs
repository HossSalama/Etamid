namespace Etamid.Application.Common.Interfaces;

public enum OtpEmailKind { Registration, PasswordReset }

public interface IEmailService
{
    /// <summary>Sends a 6-digit code. Failures are logged and not thrown, so a mail outage never breaks the API call.</summary>
    Task SendOtpEmailAsync(
        string toEmail, string recipientName, string otpCode, OtpEmailKind kind, CancellationToken cancellationToken = default);
}
