using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Identity;

namespace Etamid.Application.Identity.Services;

/// <summary>Adds security-audit rows. Callers save the context, so the row commits with their change.</summary>
public sealed class AuthAuditService(IApplicationDbContext db, IRequestContext request)
{
    public void Record(AuthEvent authEvent, long? userId, string? phoneNormalized, string? details = null)
        => db.AuthAuditLogs.Add(new AuthAuditLog
        {
            Event = authEvent,
            UserId = userId,
            PhoneNormalized = phoneNormalized,
            Ip = request.IpAddress,
            UserAgent = request.UserAgent is { Length: > 300 } ua ? ua[..300] : request.UserAgent,
            Details = details
        });
}
