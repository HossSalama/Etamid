using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Etamid.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FirebaseNotification = FirebaseAdmin.Messaging.Notification;

namespace Etamid.Infrastructure.Push;

/// <summary>
/// The only class that talks to the Firebase Admin SDK. The Firebase app is
/// created lazily, on the first send, and only in this named instance
/// (<c>etamid-fcm</c>) — the phone-verification flow does not use the Admin
/// SDK at all, so the two never share an app instance.
/// </summary>
public sealed class FirebaseMessagingClient : IFcmClient
{
    private const string AppName = "etamid-fcm";

    private readonly FcmOptions _options;
    private readonly string _projectId;
    private readonly ILogger<FirebaseMessagingClient> _logger;
    private readonly object _initLock = new();
    private FirebaseMessaging? _messaging;
    private bool _initFailed;

    public FirebaseMessagingClient(
        IOptions<FcmOptions> options,
        IOptions<FirebaseOptions> firebaseOptions,
        ILogger<FirebaseMessagingClient> logger)
    {
        _options = options.Value;
        _projectId = string.IsNullOrWhiteSpace(_options.ProjectId)
            ? firebaseOptions.Value.ProjectId
            : _options.ProjectId;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FcmSendResult>> SendMulticastAsync(
        IReadOnlyList<string> tokens, string title, string body,
        IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        var messaging = GetMessaging();
        if (messaging is null)
            return tokens.Select(_ => new FcmSendResult(false, false)).ToList();

        // MulticastMessage.Tokens is marked obsolete in FirebaseAdmin 3.7
        // ("use Fids instead"), but device tokens are what the Device table
        // stores, so tokens stay the transport. The pragma keeps the build
        // warning-clean under TreatWarningsAsErrors.
#pragma warning disable CS0618
        var message = new MulticastMessage
        {
            Tokens = tokens.ToList(),
            Notification = new FirebaseNotification { Title = title, Body = body },
            Data = new Dictionary<string, string>(data)
        };
#pragma warning restore CS0618

        try
        {
            var response = await messaging.SendEachForMulticastAsync(message, ct);
            return tokens.Select((_, i) =>
            {
                if (i >= response.Responses.Count)
                    return new FcmSendResult(false, false);
                var r = response.Responses[i];
                var invalid = r.Exception is FirebaseMessagingException fme
                    && fme.MessagingErrorCode == MessagingErrorCode.Unregistered;
                return new FcmSendResult(r.IsSuccess, invalid);
            }).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Firebase SendEachForMulticastAsync failed for {TokenCount} tokens.", tokens.Count);
            return tokens.Select(_ => new FcmSendResult(false, false)).ToList();
        }
    }

    private FirebaseMessaging? GetMessaging()
    {
        if (_messaging is not null)
            return _messaging;
        if (_initFailed)
            return null;

        lock (_initLock)
        {
            if (_messaging is not null)
                return _messaging;
            if (_initFailed)
                return null;

            try
            {
                FirebaseApp app;
                try
                {
                    app = FirebaseApp.GetInstance(AppName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Normal first-run path: no app with this name exists yet.
                    _logger.LogDebug(ex, "No Firebase app '{AppName}' yet; creating it.", AppName);
                    var credential = CredentialFactory.FromFile<ServiceAccountCredential>(_options.ServiceAccountPath).ToGoogleCredential();
                    app = FirebaseApp.Create(new AppOptions
                    {
                        Credential = credential,
                        ProjectId = _projectId
                    }, AppName);
                }

                _messaging = FirebaseMessaging.GetMessaging(app);
                return _messaging;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Bad path, unreadable or invalid service-account file, ...
                // Remember the failure so every send does not retry init and
                // spam the log; the sender reports Failed instead of throwing.
                _initFailed = true;
                _logger.LogError(ex,
                    "Could not initialize FirebaseMessaging (check Fcm:ServiceAccountPath); push notifications are unavailable.");
                return null;
            }
        }
    }
}
