namespace Etamid.Application.Common.Interfaces;

public interface ICurrentUser
{
    long? UserId { get; }
    string? UserType { get; }
    /// <summary>The refresh-token family (login session) this access token belongs to.</summary>
    Guid? SessionId { get; }
    bool IsAuthenticated { get; }
}
