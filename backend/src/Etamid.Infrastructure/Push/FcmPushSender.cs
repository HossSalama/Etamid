using Etamid.Application.Common.Interfaces;
using Etamid.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Etamid.Infrastructure.Push;

/// <summary>
/// Sends a push to a user's active devices via FCM. Best-effort by
/// contract: every failure is logged and reported as
/// <see cref="PushSendOutcome.Failed"/> — this method never throws into the
/// operation that triggered the notification.
/// </summary>
public sealed class FcmPushSender(
    IFcmClient fcm,
    IApplicationDbContext db,
    ILogger<FcmPushSender> logger) : IPushNotificationSender
{
    /// <summary>FCM accepts at most 500 tokens per multicast call.</summary>
    private const int MaxTokensPerBatch = 500;

    public async Task<PushSendOutcome> SendAsync(
        long userId, string title, string body, IReadOnlyDictionary<string, string> data,
        CancellationToken ct)
    {
        try
        {
            var tokens = await db.Devices.AsNoTracking()
                .Where(d => d.UserId == userId && d.IsActive)
                .Select(d => d.DeviceToken)
                .ToListAsync(ct);
            if (tokens.Count == 0)
                return PushSendOutcome.NoDevices;

            var anySent = false;
            var invalidTokens = new List<string>();
            foreach (var batch in tokens.Chunk(MaxTokensPerBatch))
            {
                var results = await fcm.SendMulticastAsync(batch, title, body, data, ct);
                for (var i = 0; i < batch.Length && i < results.Count; i++)
                {
                    if (results[i].Success)
                        anySent = true;
                    else if (results[i].TokenInvalid)
                        invalidTokens.Add(batch[i]);
                }
            }

            if (invalidTokens.Count > 0)
                await DeactivateAsync(invalidTokens, userId, ct);

            return anySent ? PushSendOutcome.Sent : PushSendOutcome.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "FCM push send failed for user {UserId}; the triggering operation already succeeded.", userId);
            return PushSendOutcome.Failed;
        }
    }

    /// <summary>
    /// FCM says these tokens are no longer registered (app uninstalled, token
    /// revoked): deactivate the rows so they are never retried. Other send
    /// failures (quota, FCM outage) leave the device alone — the token may
    /// still be fine.
    /// </summary>
    private async Task DeactivateAsync(List<string> invalidTokens, long userId, CancellationToken ct)
    {
        var dead = await db.Devices
            .Where(d => d.UserId == userId && invalidTokens.Contains(d.DeviceToken))
            .ToListAsync(ct);
        foreach (var device in dead)
            device.IsActive = false;

        if (dead.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Deactivated {Count} unregistered FCM device token(s) for user {UserId}.", dead.Count, userId);
        }
    }
}
