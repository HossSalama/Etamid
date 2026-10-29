using System.Net;
using System.Net.Mail;
using System.Text;
using Etamid.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Etamid.Infrastructure.Services;

public sealed class EmailService(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<EmailService> logger) : IEmailService
{
    public async Task SendOtpEmailAsync(
        string toEmail,
        string recipientName,
        string otpCode,
        OtpEmailKind kind,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(configuration["Smtp:Port"], out var p) ? p : 587;
        var senderEmail = configuration["Smtp:SenderEmail"]?.Trim() ?? string.Empty;
        var senderName = configuration["Smtp:SenderName"]?.Trim() ?? "منصة اعْتَمِدْ (Etamid)";
        var appPassword = configuration["Smtp:AppPassword"]?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(appPassword))
        {
            // Development helper only. The environment guard prevents logging secrets in production.
            if (environment.IsDevelopment() && configuration.GetValue<bool>("Smtp:LogCodeWhenDisabled"))
                logger.LogWarning("Email sending skipped (SMTP not configured). OTP for {Email} is {Otp}", toEmail, otpCode);
            else
            {
                logger.LogError("Email sending failed because SMTP credentials are not configured.");
                throw new InvalidOperationException("SMTP sender email and app password must be configured to send OTP emails.");
            }
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(senderEmail, appPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var isReset = kind == OtpEmailKind.PasswordReset;
            var introText = isReset
                ? "وصلنا طلب لاستعادة كلمة المرور الخاصة بحسابك في منصة <strong>«اعْتَمِدْ»</strong>. أدخل رمز التحقق (OTP) التالي لإكمال العملية، وتجاهل هذه الرسالة إن لم تكن أنت من طلبها:"
                : "شكراً لتسجيلك في منصة <strong>«اعْتَمِدْ»</strong>. لتأكيد وتفعيل بريدك الإلكتروني والبدء في إجراء المعاملات المالية المضمونة، يرجى إدخال رمز التحقق (OTP) التالي:";
            var badgeText = isReset ? "استعادة الحساب" : "توثيق الحساب";
            var cleanName = string.IsNullOrWhiteSpace(recipientName) ? "مستخدم اعْتَمِدْ" : recipientName.Trim();
            var initial = cleanName.Length > 0 ? cleanName[0].ToString() : "ع";

            var digitsHtml = new StringBuilder();
            foreach (var d in otpCode)
            {
                digitsHtml.Append($@"
<td style=""padding: 0 4px;"">
  <div style=""width: 44px; height: 54px; line-height: 54px; background: #ffffff; border: 2px solid #0d9488; border-radius: 10px; font-size: 28px; font-weight: 800; color: #0f766e; text-align: center; font-family: 'Courier New', Courier, monospace; box-shadow: 0 3px 6px rgba(13, 148, 136, 0.12);"">
    {d}
  </div>
</td>");
            }

            var htmlBody = $@"
<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>رمز التحقق - منصة اعْتَمِدْ</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f1f5f9; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; -webkit-font-smoothing: antialiased;"">
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color: #f1f5f9; padding: 35px 15px;"">
    <tr>
      <td align=""center"">
        <!-- Main Container -->
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""max-width: 540px; background: #ffffff; border-radius: 20px; overflow: hidden; box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.08); border: 1px solid #e2e8f0;"">
          <!-- Top Accent Bar -->
          <tr>
            <td style=""background: linear-gradient(90deg, #0d9488 0%, #14b8a6 50%, #f59e0b 100%); height: 6px;""></td>
          </tr>

          <!-- Header / Brand -->
          <tr>
            <td style=""padding: 32px 30px 16px; text-align: center;"">
              <div style=""margin-bottom: 10px;"">
                <img src=""cid:etamidLogo"" alt=""اعْتَمِدْ | etamid"" style=""height: 65px; max-height: 65px; width: auto; max-width: 220px; display: inline-block; object-fit: contain;"" />
              </div>
              <div style=""font-size: 13px; color: #64748b; font-weight: 600;"">
                المنصة السعودية للوساطة المالية وحماية المعاملات 🇸🇦
              </div>
            </td>
          </tr>

          <!-- Profile Badge -->
          <tr>
            <td style=""padding: 0 30px;"">
              <div style=""background: linear-gradient(135deg, #f0fdfa 0%, #f8fafc 100%); border: 1.5px solid #ccfbf1; border-radius: 14px; padding: 14px 18px;"">
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                  <tr>
                    <td width=""48"" style=""vertical-align: middle;"">
                      <div style=""width: 46px; height: 46px; border-radius: 50%; background: linear-gradient(135deg, #0d9488, #047857); color: #ffffff; font-size: 20px; font-weight: 800; line-height: 46px; text-align: center; border: 2px solid #ffffff; box-shadow: 0 3px 8px rgba(13, 148, 136, 0.25);"">
                        {WebUtility.HtmlEncode(initial)}
                      </div>
                    </td>
                    <td style=""vertical-align: middle; padding-right: 12px; text-align: right;"">
                      <div style=""color: #0f172a; font-size: 15px; font-weight: 800;"">
                        مرحباً، {WebUtility.HtmlEncode(cleanName)} 👋
                      </div>
                      <div style=""color: #0f766e; font-size: 12px; margin-top: 2px; font-weight: 600;"" dir=""ltr"">
                        {WebUtility.HtmlEncode(toEmail)}
                      </div>
                    </td>
                    <td style=""vertical-align: middle; text-align: left;"">
                      <span style=""display: inline-block; background: #dcfce7; color: #15803d; border: 1px solid #86efac; border-radius: 20px; padding: 4px 10px; font-size: 11px; font-weight: 700;"">
                        {badgeText} ✓
                      </span>
                    </td>
                  </tr>
                </table>
              </div>
            </td>
          </tr>

          <!-- Body Content -->
          <tr>
            <td style=""padding: 24px 30px; text-align: right;"">
              <p style=""color: #334155; font-size: 15px; line-height: 1.7; margin: 0 0 18px;"">
                {introText}
              </p>

              <!-- OTP Digits Box -->
              <div style=""background: #fafafa; border: 2px dashed #0d9488; border-radius: 16px; padding: 22px 14px; text-align: center; margin: 20px 0;"">
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" align=""center"" dir=""ltr"">
                  <tr>
                    {digitsHtml}
                  </tr>
                </table>
                <div style=""margin-top: 14px;"">
                  <span style=""display: inline-block; background: #fef3c7; color: #92400e; border: 1px solid #fde68a; border-radius: 20px; padding: 4px 12px; font-size: 12px; font-weight: 700;"">
                    ⏱️ صالح لمدة 10 دقائق فقط
                  </span>
                </div>
              </div>

              <!-- Security Notice -->
              <div style=""background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 14px 16px; margin-top: 20px;"">
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                  <tr>
                    <td width=""24"" style=""vertical-align: top; font-size: 16px;"">🔒</td>
                    <td style=""text-align: right; padding-right: 8px;"">
                      <div style=""color: #0f172a; font-size: 13px; font-weight: 700; margin-bottom: 3px;"">إرشادات أمان الحساب</div>
                      <div style=""color: #64748b; font-size: 12px; line-height: 1.6;"">
                        لا تشارك هذا الرمز مع أي شخص. موظفو منصة «اعْتَمِدْ» لن يطلبوا منك رمز التحقق الخاص بك تحت أي ظرف.
                      </div>
                    </td>
                  </tr>
                </table>
              </div>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style=""background: #f8fafc; border-top: 1px solid #e2e8f0; padding: 20px 30px; text-align: center;"">
              <p style=""margin: 0 0 6px; color: #64748b; font-size: 12px;"">
                منصة اعْتَمِدْ • وسيط مالي معتمد لحماية حقوق المشتري والبائع بالمملكة العربية السعودية 🇸🇦
              </p>
              <p style=""margin: 0; color: #94a3b8; font-size: 11px;"">
                © {DateTime.UtcNow.Year} منصة اعْتَمِدْ (Etamid). جميع الحقوق محفوظة.
              </p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = isReset ? "استعادة كلمة المرور - منصة اعْتَمِدْ" : "تفعيل الحساب - منصة اعْتَمِدْ",
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(toEmail);

            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");

            var logoCandidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Resources", "logo.png")
            };

            var logoPath = logoCandidates.FirstOrDefault(File.Exists);
            if (logoPath != null)
            {
                var logoResource = new LinkedResource(logoPath, "image/png")
                {
                    ContentId = "etamidLogo",
                    TransferEncoding = System.Net.Mime.TransferEncoding.Base64
                };
                htmlView.LinkedResources.Add(logoResource);
            }

            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Enhanced OTP email with official logo successfully dispatched to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
            throw;
        }
    }
}
