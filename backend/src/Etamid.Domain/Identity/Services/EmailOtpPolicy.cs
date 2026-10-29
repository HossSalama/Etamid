using System.Security.Cryptography;

namespace Etamid.Domain.Identity.Services;

public static class EmailOtpPolicy
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Cryptographically random, zero-padded (e.g. "004217").</summary>
    public static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static bool IsWellFormed(string? code)
        => code is { Length: CodeLength } && code.All(c => c is >= '0' and <= '9');
}
