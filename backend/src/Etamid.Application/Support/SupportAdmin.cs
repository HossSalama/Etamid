using Etamid.Application.AdminAuth;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Application.Notifications;
using Etamid.Domain.Admin;
using Etamid.Domain.Notifications;
using Etamid.Domain.Support;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Support;

// ---------- Admin DTOs ----------

public sealed record AdminSupportTicketSummaryDto(
    long Id, string Number, string Subject, string Status, string UserName, string? AssignedAdminName,
    DateTime CreatedAt, DateTime? ClosedAt, DateTime? LastMessageAt);

// ---------- GET /admin/support/tickets ----------

public sealed record ListSupportTicketsQuery(
    string? Status, long? AssignedAdminId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<AdminSupportTicketSummaryDto>>>;

public sealed class ListSupportTicketsHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListSupportTicketsQuery, Result<PagedResult<AdminSupportTicketSummaryDto>>>
{
    public async Task<Result<PagedResult<AdminSupportTicketSummaryDto>>> Handle(
        ListSupportTicketsQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.SupportManage, ct);
        if (guard.IsFailure) return guard.Error;

        if (!TicketMapper.TryParseStatusFilter(r.Status, out var statuses))
            return Error.Validation("INVALID_VALUE", "status");

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.SupportTickets.AsNoTracking();
        if (statuses is not null) query = query.Where(t => statuses.Contains(t.Status));
        if (r.AssignedAdminId is { } assignedId) query = query.Where(t => t.AssignedAdminId == assignedId);

        var total = await query.CountAsync(ct);
        var tickets = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var userIds = tickets.Select(t => t.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var adminIds = tickets.Where(t => t.AssignedAdminId is not null)
            .Select(t => t.AssignedAdminId!.Value).Distinct().ToList();
        var admins = await db.AdminUsers.AsNoTracking().Where(a => adminIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var items = new List<AdminSupportTicketSummaryDto>();
        foreach (var ticket in tickets)
        {
            var summary = await TicketMapper.SummaryAsync(db, ticket, ct);
            items.Add(new AdminSupportTicketSummaryDto(ticket.Id, ticket.Number, ticket.Subject,
                summary.Status, users.GetValueOrDefault(ticket.UserId, string.Empty),
                ticket.AssignedAdminId is { } id ? admins.GetValueOrDefault(id) : null,
                ticket.CreatedAt, ticket.ClosedAt, summary.LastMessageAt));
        }

        return new PagedResult<AdminSupportTicketSummaryDto>(items, page, pageSize, total);
    }
}

// ---------- GET /admin/support/tickets/{id} ----------

public sealed record GetAdminSupportTicketDetailQuery(long TicketId)
    : IRequest<Result<SupportTicketDetailDto>>;

public sealed class GetAdminSupportTicketDetailHandler(
    IApplicationDbContext db, AdminAccessService access, IFileUrlResolver files)
    : IRequestHandler<GetAdminSupportTicketDetailQuery, Result<SupportTicketDetailDto>>
{
    public async Task<Result<SupportTicketDetailDto>> Handle(
        GetAdminSupportTicketDetailQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.SupportManage, ct);
        if (guard.IsFailure) return guard.Error;

        var ticket = await db.SupportTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == r.TicketId, ct);
        if (ticket is null) return SupportErrors.NotFound;
        return await TicketMapper.DetailAsync(db, ticket, ct, files);
    }
}

// ---------- POST /admin/support/tickets/{id}/assign ----------

public sealed record AssignSupportTicketCommand(long TicketId, long AdminId)
    : IRequest<Result<SupportTicketDetailDto>>;

public sealed class AssignSupportTicketHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit, IFileUrlResolver files)
    : IRequestHandler<AssignSupportTicketCommand, Result<SupportTicketDetailDto>>
{
    public async Task<Result<SupportTicketDetailDto>> Handle(AssignSupportTicketCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.SupportManage, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == r.TicketId, ct);
        if (ticket is null) return SupportErrors.NotFound;
        if (ticket.Status == TicketStatus.Closed) return SupportErrors.InvalidState;

        var assignee = await db.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == r.AdminId, ct);
        if (assignee is null) return Error.Validation("INVALID_VALUE", "adminId");

        var oldStatus = ticket.Status;
        ticket.AssignedAdminId = assignee.Id;
        if (ticket.Status == TicketStatus.Open) ticket.Status = TicketStatus.InReview;

        audit.Record(AuditActorType.Admin, admin.Id, "admin.support.assign", "SupportTicket", ticket.Id,
            new { status = oldStatus.ToString(), assignedAdminId = (long?)null },
            new { status = ticket.Status.ToString(), assignedAdminId = assignee.Id });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await TicketMapper.DetailAsync(db, ticket, ct, files);
    }
}

// ---------- POST /admin/support/tickets/{id}/messages ----------

public sealed record PostAdminSupportTicketMessageCommand(long TicketId, string? Message)
    : IRequest<Result<SupportTicketMessageDto>>;

public sealed class PostAdminSupportTicketMessageValidator : AbstractValidator<PostAdminSupportTicketMessageCommand>
{
    public PostAdminSupportTicketMessageValidator()
    {
        RuleFor(x => x.Message!).MaximumLength(1000).WithErrorCode("TOO_LONG").When(x => x.Message is not null);
    }
}

public sealed class PostAdminSupportTicketMessageHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit,
    NotificationService notifications, IFileUrlResolver files)
    : IRequestHandler<PostAdminSupportTicketMessageCommand, Result<SupportTicketMessageDto>>
{
    public async Task<Result<SupportTicketMessageDto>> Handle(
        PostAdminSupportTicketMessageCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.SupportManage, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        if (string.IsNullOrWhiteSpace(r.Message)) return Error.Validation("INVALID_VALUE", "message");

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == r.TicketId, ct);
        if (ticket is null) return SupportErrors.NotFound;
        if (ticket.Status == TicketStatus.Closed) return SupportErrors.InvalidState;

        var message = new SupportTicketMessage
        {
            TicketId = ticket.Id,
            SenderType = TicketSenderType.Admin,
            SenderAdminId = admin.Id,
            Message = r.Message.Trim()
        };
        db.SupportTicketMessages.Add(message);

        // An admin answer moves the ticket into review and notifies its owner.
        var oldStatus = ticket.Status;
        if (ticket.Status == TicketStatus.Open) ticket.Status = TicketStatus.InReview;
        notifications.Create(ticket.UserId, NotificationType.SupportTicket,
            "NOTIF_SUPPORT_TICKET_REPLY_TITLE", "NOTIF_SUPPORT_TICKET_REPLY_BODY", ticket.Number);

        audit.Record(AuditActorType.Admin, admin.Id, "admin.support.message", "SupportTicket", ticket.Id,
            new { status = oldStatus.ToString() },
            new { status = ticket.Status.ToString() });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return new SupportTicketMessageDto(message.Id, ApiNames.Of(message.SenderType),
            admin.Name, message.Message, null, message.CreatedAt);
    }
}

// ---------- POST /admin/support/tickets/{id}/close ----------

public sealed record CloseAdminSupportTicketCommand(long TicketId)
    : IRequest<Result<SupportTicketDetailDto>>;

public sealed class CloseAdminSupportTicketHandler(
    IApplicationDbContext db, AdminAccessService access, AdminAuditService audit,
    NotificationService notifications, IFileUrlResolver files)
    : IRequestHandler<CloseAdminSupportTicketCommand, Result<SupportTicketDetailDto>>
{
    public async Task<Result<SupportTicketDetailDto>> Handle(
        CloseAdminSupportTicketCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.SupportManage, ct);
        if (guard.IsFailure) return guard.Error;
        var admin = guard.Value;

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == r.TicketId, ct);
        if (ticket is null) return SupportErrors.NotFound;
        if (ticket.Status == TicketStatus.Closed) return SupportErrors.InvalidState;

        var oldStatus = ticket.Status;
        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTime.UtcNow;
        notifications.Create(ticket.UserId, NotificationType.SupportTicket,
            "NOTIF_SUPPORT_TICKET_CLOSED_TITLE", "NOTIF_SUPPORT_TICKET_CLOSED_BODY", ticket.Number);

        audit.Record(AuditActorType.Admin, admin.Id, "admin.support.close", "SupportTicket", ticket.Id,
            new { status = oldStatus.ToString() },
            new { status = ticket.Status.ToString() });

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return await TicketMapper.DetailAsync(db, ticket, ct, files);
    }
}
