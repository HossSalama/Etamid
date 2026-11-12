using Etamid.Application.Common.Interfaces;
using OtpNet;

namespace Etamid.Infrastructure.Identity;

/// <summary>RFC 6238 TOTP via Otp.NET: SHA-1, 6 digits, 30 s step, one step of drift either way.</summary>
public sealed class OtpNetTotpService : ITotpService
{
    public string NewSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public bool Verify(string base32Secret, string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 6 || code.Any(c => c is < '0' or > '9')) return false;

        byte[] key;
        try { key = Base32Encoding.ToBytes(base32Secret); }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }

        var totp = new Totp(key, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }

    public string BuildOtpAuthUri(string issuer, string account, string base32Secret)
    {
        var i = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{i}:{Uri.EscapeDataString(account)}?secret={base32Secret}&issuer={i}&algorithm=SHA1&digits=6&period=30";
    }
}
