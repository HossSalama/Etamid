namespace Etamid.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "etamid";
    public string Audience { get; set; } = "etamid-clients";
    /// <summary>At least 32 characters. Provide it from user-secrets or an environment variable, never from git.</summary>
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
