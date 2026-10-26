using System.Security.Cryptography;
using System.Text;

namespace Etamid.Application.Identity.Services;

/// <summary>Refresh tokens are random secrets; only their SHA-256 hash is stored.</summary>
public static class TokenHasher
{
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
