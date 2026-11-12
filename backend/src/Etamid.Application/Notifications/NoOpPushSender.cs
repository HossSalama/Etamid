namespace Etamid.Application.Notifications;

/// <summary>
/// Push is not configured (FCM disabled or no service-account credentials):
/// sending is a no-op. Registered by default; the infrastructure layer
/// replaces this registration with <c>FcmPushSender</c> when FCM is enabled.
/// </summary>
public sealed class NoOpPushSender : IPushNotificationSender
{
    public Task<PushSendOutcome> SendAsync(
        long userId, string title, string body, IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
        => Task.FromResult(PushSendOutcome.Disabled);
}
