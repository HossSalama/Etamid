namespace Etamid.Infrastructure.Push;

/// <summary>One send attempt against a single device token.</summary>
public sealed record FcmSendResult(bool Success, bool TokenInvalid);

/// <summary>
/// Thin abstraction over <c>FirebaseMessaging</c> so the sender can be
/// tested without real Firebase: tests substitute this interface, the
/// production implementation is the only place that touches the Admin SDK.
/// </summary>
public interface IFcmClient
{
    /// <summary>
    /// Sends one message to many tokens. Returns one result per token, in
    /// the same order as <paramref name="tokens"/>. Never throws: a
    /// transport failure is reported as failed results.
    /// </summary>
    Task<IReadOnlyList<FcmSendResult>> SendMulticastAsync(
        IReadOnlyList<string> tokens, string title, string body,
        IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken);
}
