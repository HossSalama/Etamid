using System.Security.Claims;
using System.Text;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Etamid.Infrastructure.Identity;

public sealed class AdminJwtOptions
{
    public const string Section = "AdminJwt";

    public string Issuer { get; set; } = "etamid-admin";
    public string Audience { get; set; } = "etamid-admin-clients";
    /// <summary>At least 32 characters, different from Jwt:Secret. User-secrets or environment only, never git.</summary>
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
}

/// <summary>Admin access token. Claims: sub = admin id, type = "admin", sid = session family, jti. No permissions.</summary>
public sealed class JwtAdminAccessTokenGenerator(IOptions<AdminJwtOptions> options, IClock clock) : IAdminAccessTokenGenerator
{
    public AdminAccessToken Generate(AdminUser admin, Guid sessionId)
    {
        var o = options.Value;
        var now = clock.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Secret));

        var claims = new List<Claim>
        {
            new("sub", admin.Id.ToString()),
            new("type", "admin"),
            new("sid", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(o.AccessTokenMinutes),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AdminAccessToken(token, o.AccessTokenMinutes * 60);
    }
}
