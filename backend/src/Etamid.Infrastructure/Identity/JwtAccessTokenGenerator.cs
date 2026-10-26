using System.Security.Claims;
using System.Text;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Etamid.Infrastructure.Identity;

public sealed class JwtAccessTokenGenerator(IOptions<JwtOptions> options, IClock clock) : IAccessTokenGenerator
{
    public AccessToken Generate(User user, Guid sessionId)
    {
        var o = options.Value;
        var now = clock.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Secret));

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("type", user.Type.ToString().ToLowerInvariant()),
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
        return new AccessToken(token, o.AccessTokenMinutes * 60);
    }
}
