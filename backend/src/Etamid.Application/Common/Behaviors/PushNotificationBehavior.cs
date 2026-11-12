using System.Globalization;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Notifications;
using Etamid.Domain.Notifications;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Etamid.Application.Common.Behaviors;

/// <summary>
/// Push (Layer 2) choke point. Runs after a MediatR handler returns, and
/// sends a push for every in-app notification the handler created in this
/// scope — without touching any handler.
///
/// Why a pipeline behavior, and not the alternatives:
/// - (a) a post-save call at every call site would touch ~20 places across
///   deals, escrow, disputes and company deals, and every future handler
///   would have to remember it. Rejected.
/// - (b) a DbContext SaveChanges interceptor fires mid-save, before the
///   outer operation is known to have succeeded, and doing network I/O plus
///   a second save from inside SaveChanges is reentrancy-prone (it would
///   also fire for its own PushSentAt save). Rejected.
/// - (c) this behavior: it sees the handler's final Result, so a failed or
///   rolled-back operation sends nothing, and it runs after the handler's
///   own save, so it only pushes for rows that actually committed.
///
/// Tradeoff: a notification created outside MediatR (none exists today —
/// every Create call site is a MediatR handler, including the background
/// maintenance worker, which goes through ISender) would not get a push.
/// And a push is sent at most once per request: if the send fails there is
/// no retry yet — the row keeps PushSentAt null so a future outbox/retry
/// sweep can pick it up.
///
/// The behavior never fails the request: any error while draining is logged
/// and swallowed. The triggering operation already succeeded at that point.
/// </summary>
public sealed class PushNotificationBehavior<TRequest, TResponse>(
    NotificationService notifications,
    IApplicationDbContext db,
    IPushNotificationSender pushSender,
    IClock clock,
    ILogger<PushNotificationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        // A failed result means the operation did not commit: never push for
        // an event that did not happen. (An exception from the handler never
        // reaches here — it propagates before the drain.)
        if (response is Result { IsFailure: true })
            return response;

        try
        {
            await DrainAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Push notification drain failed for {Request}; the request itself succeeded.",
                typeof(TRequest).Name);
        }

        return response;
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        if (notifications.PendingNotifications.Count == 0)
            return;

        var stamped = false;
        foreach (var notification in notifications.PendingNotifications)
        {
            // Only push for rows that actually committed. EF gives a new
            // entity a temporary key (<= 0) until its save; a notification
            // whose handler returned without saving — or one created by an
            // inner Send inside a not-yet-saved outer handler — stays
            // pending and is simply never sent.
            if (notification.Id <= 0)
                continue;

            var outcome = await pushSender.SendAsync(
                notification.UserId, notification.TitleAr, notification.BodyAr,
                PushData(notification), ct);
            notifications.MarkPushHandled(notification);

            // Stamp only when a push was really attempted or there was
            // nothing to send to. Failed and Disabled keep PushSentAt null,
            // so a future retry sweep can still pick the row up.
            if (outcome is PushSendOutcome.Sent or PushSendOutcome.NoDevices)
            {
                notification.PushSentAt = clock.UtcNow;
                stamped = true;
            }
        }

        if (stamped)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deep-link data for the client. The visible push carries Arabic
    /// (TitleAr/BodyAr — the marketplace's primary language; no per-user
    /// language is stored), while the data payload carries both languages
    /// so the app can re-render in the user's language on tap, plus the ids
    /// the app needs to open the right screen. FCM data values must be
    /// strings, so ids are sent as invariant-culture strings.
    /// </summary>
    private static Dictionary<string, string> PushData(Notification notification)
    {
        var data = new Dictionary<string, string>
        {
            ["type"] = notification.Type.ToString(),
            ["titleAr"] = notification.TitleAr,
            ["titleEn"] = notification.TitleEn,
            ["bodyAr"] = notification.BodyAr,
            ["bodyEn"] = notification.BodyEn
        };
        if (notification.DealId is { } dealId)
            data["dealId"] = dealId.ToString(CultureInfo.InvariantCulture);
        if (notification.CompanyDealId is { } companyDealId)
            data["companyDealId"] = companyDealId.ToString(CultureInfo.InvariantCulture);
        if (notification.InstallmentId is { } installmentId)
            data["installmentId"] = installmentId.ToString(CultureInfo.InvariantCulture);
        if (notification.DisputeId is { } disputeId)
            data["disputeId"] = disputeId.ToString(CultureInfo.InvariantCulture);
        return data;
    }
}
