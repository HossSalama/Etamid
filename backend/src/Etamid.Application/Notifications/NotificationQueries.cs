using Etamid.Application.Common;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Notifications;

public sealed record NotificationDto(
    long Id, string Type, string Title, string Body, bool IsRead,
    long? DealId, long? CompanyDealId, long? InstallmentId, long? DisputeId, DateTime CreatedAt);

public sealed record UnreadNotificationsDto(int Count);

public sealed record ReadAllNotificationsDto(int MarkedCount);

// ======================================================================= List
public sealed record GetNotificationsQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<NotificationDto>>>;

public sealed class GetNotificationsHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetNotificationsQuery, Result<PagedResult<NotificationDto>>>
{
    public async Task<Result<PagedResult<NotificationDto>>> Handle(GetNotificationsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<NotificationDto>(
            items.Select(n => new NotificationDto(
                n.Id, ApiNames.Of(n.Type),
                LanguageHelper.Pick(n.TitleAr, n.TitleEn), LanguageHelper.Pick(n.BodyAr, n.BodyEn),
                n.IsRead, n.DealId, n.CompanyDealId, n.InstallmentId, n.DisputeId, n.CreatedAt)).ToList(),
            page, pageSize, total);
    }
}

// ======================================================================= Unread count
public sealed record GetUnreadCountQuery : IRequest<Result<UnreadNotificationsDto>>;

public sealed class GetUnreadCountHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetUnreadCountQuery, Result<UnreadNotificationsDto>>
{
    public async Task<Result<UnreadNotificationsDto>> Handle(GetUnreadCountQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return new UnreadNotificationsDto(count);
    }
}

// ======================================================================= Mark one read
public sealed record MarkNotificationReadCommand(long Id) : IRequest<Result<NotificationDto>>;

public sealed class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadValidator() => RuleFor(x => x.Id).GreaterThan(0).WithErrorCode("REQUIRED");
}

public sealed class MarkNotificationReadHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<MarkNotificationReadCommand, Result<NotificationDto>>
{
    public async Task<Result<NotificationDto>> Handle(MarkNotificationReadCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        // Any id that is not the caller's own notification is 404: ids cannot be probed.
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == r.Id && n.UserId == userId, ct);
        if (notification is null) return CommonErrors.NotFound;

        // Already read is an idempotent success, not an error.
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return ToDto(notification);
    }

    internal static NotificationDto ToDto(Etamid.Domain.Notifications.Notification n) => new(
        n.Id, ApiNames.Of(n.Type),
        LanguageHelper.Pick(n.TitleAr, n.TitleEn), LanguageHelper.Pick(n.BodyAr, n.BodyEn),
        n.IsRead, n.DealId, n.CompanyDealId, n.InstallmentId, n.DisputeId, n.CreatedAt);
}

// ======================================================================= Mark all read
public sealed record MarkAllNotificationsReadCommand : IRequest<Result<ReadAllNotificationsDto>>;

public sealed class MarkAllNotificationsReadHandler(IApplicationDbContext db, ICurrentUser currentUser, IClock clock)
    : IRequestHandler<MarkAllNotificationsReadCommand, Result<ReadAllNotificationsDto>>
{
    public async Task<Result<ReadAllNotificationsDto>> Handle(MarkAllNotificationsReadCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        if (unread.Count == 0) return new ReadAllNotificationsDto(0);

        var now = clock.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }
        await db.SaveChangesAsync(ct);
        return new ReadAllNotificationsDto(unread.Count);
    }
}
