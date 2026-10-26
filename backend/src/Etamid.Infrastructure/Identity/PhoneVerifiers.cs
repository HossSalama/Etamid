using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Errors;
using Etamid.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Etamid.Infrastructure.Identity;

public sealed class FirebaseOptions
{
    public const string Section = "Firebase";

    /// <summary>The Firebase project id (Project settings in the Firebase console).</summary>
    public string ProjectId { get; set; } = string.Empty;
}

/// <summary>
/// Validates a Firebase Phone Auth ID token: signature (Google public keys), issuer, audience, expiry,
/// then reads the verified phone number and the time the SMS check was completed.
/// </summary>
public sealed class FirebasePhoneVerifier : IPhoneVerifier
{
    private readonly string _projectId;
    private readonly ILogger<FirebasePhoneVerifier> _logger;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuration;

    public FirebasePhoneVerifier(IOptions<FirebaseOptions> options, ILogger<FirebasePhoneVerifier> logger)
    {
        _projectId = options.Value.ProjectId;
        _logger = logger;
        _configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"https://securetoken.google.com/{_projectId}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<Result<VerifiedPhone>> VerifyAsync(string firebaseIdToken, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _configuration.GetConfigurationAsync(cancellationToken);

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://securetoken.google.com/{_projectId}",
                ValidateAudience = true,
                ValidAudience = _projectId,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = config.SigningKeys
            };

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(firebaseIdToken, parameters);
            if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
                return IdentityErrors.PhoneTokenInvalid;

            if (!jwt.TryGetPayloadValue<string>("phone_number", out var phone) || string.IsNullOrWhiteSpace(phone))
                return IdentityErrors.PhoneTokenInvalid;

            if (!jwt.TryGetPayloadValue<long>("auth_time", out var authTime))
                return IdentityErrors.PhoneTokenInvalid;

            return new VerifiedPhone(phone, DateTimeOffset.FromUnixTimeSeconds(authTime).UtcDateTime);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Firebase token validation failed");
            return IdentityErrors.PhoneTokenInvalid;
        }
    }
}

/// <summary>
/// Development/testing only. Accepts tokens shaped "fake:+9665XXXXXXXX" so the whole flow can run
/// without Firebase. Never registered in Production.
/// </summary>
public sealed class FakePhoneVerifier(IClock clock) : IPhoneVerifier
{
    private const string Prefix = "fake:";

    public Task<Result<VerifiedPhone>> VerifyAsync(string firebaseIdToken, CancellationToken cancellationToken)
    {
        if (firebaseIdToken is null || !firebaseIdToken.StartsWith(Prefix, StringComparison.Ordinal))
            return Task.FromResult<Result<VerifiedPhone>>(IdentityErrors.PhoneTokenInvalid);

        var phone = firebaseIdToken[Prefix.Length..];
        return Task.FromResult<Result<VerifiedPhone>>(new VerifiedPhone(phone, clock.UtcNow));
    }
}
