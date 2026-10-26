using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;

namespace Etamid.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
    /// <summary>Burns the same time as a real check, so unknown phones cannot be told apart by response time.</summary>
    void VerifyDummy(string password);
}

public sealed record AccessToken(string Token, int ExpiresInSeconds);

public interface IAccessTokenGenerator
{
    AccessToken Generate(User user, Guid sessionId);
}

/// <summary>The phone number proven by a Firebase Phone Auth ID token, and when the SMS check happened.</summary>
public sealed record VerifiedPhone(string PhoneE164, DateTime AuthTimeUtc);

public interface IPhoneVerifier
{
    Task<Result<VerifiedPhone>> VerifyAsync(string firebaseIdToken, CancellationToken cancellationToken);
}

public interface IFileUrlResolver
{
    string? Resolve(StoredFile? file);
}

public interface IRequestContext
{
    string? IpAddress { get; }
    string? UserAgent { get; }
}
