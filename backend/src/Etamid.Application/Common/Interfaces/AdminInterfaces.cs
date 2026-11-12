using Etamid.Domain.Admin;

namespace Etamid.Application.Common.Interfaces;

/// <summary>The signed-in admin (AdminJwt scheme only). Never use ICurrentUser for admins.</summary>
public interface ICurrentAdmin
{
    long? AdminId { get; }
    /// <summary>The admin refresh-token family (login session) this access token belongs to.</summary>
    Guid? SessionId { get; }
    bool IsAuthenticated { get; }
}

public sealed record AdminAccessToken(string Token, int ExpiresInSeconds);

public interface IAdminAccessTokenGenerator
{
    AdminAccessToken Generate(AdminUser admin, Guid sessionId);
}

/// <summary>RFC 6238 TOTP (30 s step, +/-1 step drift), backed by a vetted library.</summary>
public interface ITotpService
{
    /// <summary>New random secret, Base32.</summary>
    string NewSecret();
    bool Verify(string base32Secret, string code);
    string BuildOtpAuthUri(string issuer, string account, string base32Secret);
}
