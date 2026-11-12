using Etamid.Infrastructure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Etamid.Infrastructure.Push;

/// <summary>
/// Logs the effective FCM state once at startup. Never throws: a missing or
/// bad push configuration must not take the API down — the sender simply
/// stays a no-op (see AddInfrastructure.AddPushNotifications).
/// </summary>
public sealed class FcmStartupLogHostedService(
    IOptions<FcmOptions> options,
    IOptions<FirebaseOptions> firebaseOptions,
    ILogger<FcmStartupLogHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var fcm = options.Value;
            if (!fcm.Enabled)
            {
                logger.LogInformation("FCM push notifications are disabled (Fcm:Enabled is false).");
            }
            else if (string.IsNullOrWhiteSpace(fcm.ServiceAccountPath))
            {
                logger.LogWarning("FCM push is enabled but Fcm:ServiceAccountPath is empty; push notifications are disabled.");
            }
            else if (!File.Exists(fcm.ServiceAccountPath))
            {
                logger.LogWarning(
                    "FCM push is enabled but the service-account file was not found at {Path}; push notifications are disabled.",
                    fcm.ServiceAccountPath);
            }
            else
            {
                var projectId = string.IsNullOrWhiteSpace(fcm.ProjectId)
                    ? firebaseOptions.Value.ProjectId
                    : fcm.ProjectId;
                logger.LogInformation("FCM push notifications enabled for project {ProjectId}.", projectId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not report the FCM push state at startup.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
