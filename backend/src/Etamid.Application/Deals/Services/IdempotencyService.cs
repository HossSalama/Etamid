using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Money;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Deals.Services;

public sealed record IdempotencyReplay(bool KeyReusedForOtherRequest, string? ResponseBody)
{
    public bool IsReplay => ResponseBody is not null;
}

/// <summary>
/// Stores the answer of a money-moving request under (user, X-Idempotency-Key). The same key with the
/// same request returns the stored answer without doing the work again; the same key for a different
/// request is rejected.
/// </summary>
public sealed class IdempotencyService(IApplicationDbContext db, IClock clock)
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<IdempotencyReplay> FindAsync(long userId, string key, string requestHash, CancellationToken ct)
    {
        var record = await db.IdempotencyKeys.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, ct);
        if (record is null) return new IdempotencyReplay(false, null);
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal)) return new IdempotencyReplay(true, null);
        return new IdempotencyReplay(false, record.ResponseBody);
    }

    /// <summary>Stages the record; it is saved by the caller together with the work it protects.</summary>
    public void Stage(long userId, string key, string endpoint, string requestHash, int responseStatus, string responseBody)
    {
        var now = clock.UtcNow;
        db.IdempotencyKeys.Add(new IdempotencyRecord
        {
            UserId = userId,
            Key = key,
            RequestHash = requestHash,
            Endpoint = endpoint,
            ResponseStatus = responseStatus,
            ResponseBody = responseBody,
            CreatedAt = now,
            ExpiresAt = now + Retention
        });
    }
}
