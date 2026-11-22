using Etamid.Application.AdminAuth;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Admin;
using Etamid.Domain.Support;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Support;

// ---------- DTO ----------

public sealed record GuestContactMessageDto(
    long Id, string Name, string Channel, string Subject, string Message, DateTime CreatedAt);

// ---------- POST /support/contact-public (anonymous, website contact form) ----------

public sealed record CreateGuestContactMessageCommand(
    string? Name, string? Channel, string? Subject, string? Message)
    : IRequest<Result<GuestContactMessageDto>>;

public sealed class CreateGuestContactMessageValidator : AbstractValidator<CreateGuestContactMessageCommand>
{
    public CreateGuestContactMessageValidator()
    {
        RuleFor(x => x.Name!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Name is not null);
        RuleFor(x => x.Channel!).MaximumLength(254).WithErrorCode("TOO_LONG").When(x => x.Channel is not null);
        RuleFor(x => x.Subject!).MaximumLength(150).WithErrorCode("TOO_LONG").When(x => x.Subject is not null);
        RuleFor(x => x.Message!).MaximumLength(2000).WithErrorCode("TOO_LONG").When(x => x.Message is not null);
    }
}

public sealed class CreateGuestContactMessageHandler(IApplicationDbContext db)
    : IRequestHandler<CreateGuestContactMessageCommand, Result<GuestContactMessageDto>>
{
    public async Task<Result<GuestContactMessageDto>> Handle(
        CreateGuestContactMessageCommand r, CancellationToken ct)
    {
        // No login: the website contact form is public. Every field is required;
        // the message is stored as a guest message, never as a SupportTicket.
        if (string.IsNullOrWhiteSpace(r.Name)) return Error.Validation("INVALID_VALUE", "name");
        if (string.IsNullOrWhiteSpace(r.Channel)) return Error.Validation("INVALID_VALUE", "channel");
        if (string.IsNullOrWhiteSpace(r.Subject)) return Error.Validation("INVALID_VALUE", "subject");
        if (string.IsNullOrWhiteSpace(r.Message)) return Error.Validation("INVALID_VALUE", "message");

        var guest = new GuestContactMessage
        {
            Name = r.Name.Trim(),
            Channel = r.Channel.Trim(),
            Subject = r.Subject.Trim(),
            Message = r.Message.Trim()
        };
        db.GuestContactMessages.Add(guest);
        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        return new GuestContactMessageDto(guest.Id, guest.Name, guest.Channel,
            guest.Subject, guest.Message, guest.CreatedAt);
    }
}

// ---------- GET /admin/support/contact-messages ----------

public sealed record ListGuestContactMessagesQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<GuestContactMessageDto>>>;

public sealed class ListGuestContactMessagesHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<ListGuestContactMessagesQuery, Result<PagedResult<GuestContactMessageDto>>>
{
    public async Task<Result<PagedResult<GuestContactMessageDto>>> Handle(
        ListGuestContactMessagesQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.SupportManage, ct);
        if (guard.IsFailure) return guard.Error;

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.GuestContactMessages.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(m => new GuestContactMessageDto(
            m.Id, m.Name, m.Channel, m.Subject, m.Message, m.CreatedAt)).ToList();
        return new PagedResult<GuestContactMessageDto>(items, page, pageSize, total);
    }
}

// ---------- GET /admin/support/contact-messages/{id} ----------

public sealed record GetGuestContactMessageDetailQuery(long MessageId)
    : IRequest<Result<GuestContactMessageDto>>;

public sealed class GetGuestContactMessageDetailHandler(IApplicationDbContext db, AdminAccessService access)
    : IRequestHandler<GetGuestContactMessageDetailQuery, Result<GuestContactMessageDto>>
{
    public async Task<Result<GuestContactMessageDto>> Handle(
        GetGuestContactMessageDetailQuery r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(AdminPermissionCodes.SupportManage, ct);
        if (guard.IsFailure) return guard.Error;

        var row = await db.GuestContactMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == r.MessageId, ct);
        if (row is null) return SupportErrors.GuestContactNotFound;
        return new GuestContactMessageDto(row.Id, row.Name, row.Channel,
            row.Subject, row.Message, row.CreatedAt);
    }
}
