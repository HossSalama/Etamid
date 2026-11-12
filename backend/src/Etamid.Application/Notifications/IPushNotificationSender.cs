namespace Etamid.Application.Notifications;

/// <summary>
/// Outcome of a push attempt, so callers can tell "nothing was sent because
/// push is off" apart from "we tried and it failed". The sender never throws:
/// every failure is reported as <see cref="PushSendOutcome.Failed"/>.
/// </summary>
public enum PushSendOutcome
{
    /// <summary>Push is not configured (FCM disabled or no credentials): nothing was attempted.</summary>
    Disabled,
    /// <summary>At least one device accepted the message.</summary>
    Sent,
    /// <summary>The user has no active devices: nothing to send to.</summary>
    NoDevices,
    /// <summary>Every send failed (FCM unreachable, bad credentials, ...). Safe to retry later.</summary>
    Failed
}

/// <summary>
/// Sends a push notification to a user's registered devices (Layer 2).
/// Implementations must be best-effort: they log failures and return
/// <see cref="PushSendOutcome.Failed"/> instead of throwing, so a push
/// problem can never fail the deal, dispute or wallet operation that
/// triggered the notification.
/// </summary>
public interface IPushNotificationSender
{
    Task<PushSendOutcome> SendAsync(
        long userId, string title, string body, IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken);
}
