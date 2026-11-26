using Etamid.Application.Common;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Application.Deals.Services;
using Etamid.Domain.Deals;
using Etamid.Domain.Files;
using Etamid.Domain.Support;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Support;

// ---------- Errors ----------

public static class SupportErrors
{
    public static readonly Error NotFound = Error.NotFound("SUPPORT_TICKET_NOT_FOUND");
    public static readonly Error InvalidState = Error.Conflict("SUPPORT_TICKET_INVALID_STATE");
    public static readonly Error GuestContactNotFound = Error.NotFound("GUEST_CONTACT_NOT_FOUND");
}

// ---------- DTOs ----------

public sealed record SupportTicketSummaryDto(
    long Id, string Number, string Subject, string Status, DateTime CreatedAt, DateTime? ClosedAt,
    DateTime? LastMessageAt);

public sealed record SupportTicketMessageDto(
    long Id, string SenderType, string SenderName, string Message, string? FileUrl, DateTime CreatedAt);

public sealed record SupportTicketDetailDto(
    long Id, string Number, string Subject, string Status, long? DealId, string? DealTitle,
    string UserName, string? AssignedAdminName, DateTime CreatedAt, DateTime? ClosedAt,
    IReadOnlyList<SupportTicketMessageDto> Messages);

// ---------- Mapper ----------

public static class TicketMapper
{
    public static async Task<SupportTicketSummaryDto> SummaryAsync(
        IApplicationDbContext db, SupportTicket ticket, CancellationToken ct)
    {
        var lastMessageAt = await db.SupportTicketMessages.AsNoTracking()
            .Where(m => m.TicketId == ticket.Id)
            .MaxAsync(m => (DateTime?)m.CreatedAt, ct);
        return new SupportTicketSummaryDto(ticket.Id, ticket.Number, ticket.Subject,
            ApiNames.Of(ticket.Status), ticket.CreatedAt, ticket.ClosedAt, lastMessageAt);
    }

    public static async Task<SupportTicketDetailDto> DetailAsync(
        IApplicationDbContext db, SupportTicket ticket, CancellationToken ct,
        IFileUrlResolver? files = null)
    {
        var messages = await db.SupportTicketMessages.AsNoTracking()
            .Where(m => m.TicketId == ticket.Id)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync(ct);

        var userIds = messages.Where(m => m.SenderUserId is not null).Select(m => m.SenderUserId!.Value)
            .Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var adminIds = messages.Where(m => m.SenderAdminId is not null).Select(m => m.SenderAdminId!.Value)
            .Distinct().ToList();
        var admins = await db.AdminUsers.AsNoTracking().Where(a => adminIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, ct);
        var fileIds = messages.Where(m => m.FileId is not null).Select(m => m.FileId!.Value)
            .Distinct().ToList();
        var fileMap = fileIds.Count == 0
            ? new Dictionary<long, StoredFile>()
            : await db.Files.AsNoTracking().Where(f => fileIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, ct);

        var owner = await db.Users.AsNoTracking()
            .Where(u => u.Id == ticket.UserId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        var assignedAdmin = ticket.AssignedAdminId is { } assignedId
            ? await db.AdminUsers.AsNoTracking()
                .Where(a => a.Id == assignedId).Select(a => a.Name).FirstOrDefaultAsync(ct)
            : null;
        var dealTitle = ticket.DealId is { } dealId
            ? await db.Deals.AsNoTracking()
                .Where(d => d.Id == dealId).Select(d => d.Title).FirstOrDefaultAsync(ct)
            : null;

        var messageDtos = messages.Select(m =>
        {
            var senderName = m.SenderType == TicketSenderType.Admin
                ? admins.GetValueOrDefault(m.SenderAdminId ?? 0, string.Empty)
                : users.GetValueOrDefault(m.SenderUserId ?? 0, string.Empty);
            var file = m.FileId is { } fileId ? fileMap.GetValueOrDefault(fileId) : null;
            return new SupportTicketMessageDto(m.Id, ApiNames.Of(m.SenderType), senderName,
                m.Message, file is null ? null : files?.Resolve(file), m.CreatedAt);
        }).ToList();

        return new SupportTicketDetailDto(ticket.Id, ticket.Number, ticket.Subject,
            ApiNames.Of(ticket.Status), ticket.DealId, dealTitle, owner ?? string.Empty,
            assignedAdmin, ticket.CreatedAt, ticket.ClosedAt, messageDtos);
    }

    /// <summary>
    /// The mobile "open" tab means everything not closed (Open + InReview); "closed" is
    /// Closed only. "in_review" narrows to InReview. Anything else is INVALID_VALUE.
    /// </summary>
    public static bool TryParseStatusFilter(string? status, out TicketStatus[]? statuses)
    {
        statuses = null;
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
            return true;
        if (status.Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            statuses = [TicketStatus.Open, TicketStatus.InReview];
            return true;
        }
        if (status.Equals("in_review", StringComparison.OrdinalIgnoreCase))
        {
            statuses = [TicketStatus.InReview];
            return true;
        }
        if (status.Equals("closed", StringComparison.OrdinalIgnoreCase))
        {
            statuses = [TicketStatus.Closed];
            return true;
        }
        return false;
    }
}

// ---------- Shared creation ----------

internal static class SupportTicketCreation
{
    /// <summary>Resolves the ticket subject: a ticket_subject lookup row (preferred, feeds the
    /// contact-form dropdown) or free text. Returns the stored subject string, or null.</summary>
    public static async Task<string?> ResolveSubjectAsync(
        IApplicationDbContext db, long? subjectId, string? subject, CancellationToken ct)
    {
        if (subjectId is { } id)
        {
            var lookup = await db.Lookups.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id && l.Type == "ticket_subject" && l.IsActive, ct);
            if (lookup is null) return null;
            return LanguageHelper.Pick(lookup.NameAr, lookup.NameEn);
        }
        if (!string.IsNullOrWhiteSpace(subject)) return subject.Trim();
        return null;
    }

    public static async Task<Result<SupportTicket>> InsertAsync(
        IApplicationDbContext db, long userId, long? subjectId, string subject, string message, long? dealId,
        CancellationToken ct)
    {
        // Number is unique and the id is only known after the insert: a throwaway temp
        // value first, then SUP-{id} once the row exists. The temp value never leaves the handler.
        var ticket = new SupportTicket
        {
            Number = "TMP-" + Guid.NewGuid().ToString("N")[..8],
            UserId = userId,
            DealId = dealId,
            SubjectId = subjectId,
            Subject = subject,
            Message = message,
            Status = TicketStatus.Open
        };
        db.SupportTickets.Add(ticket);
        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        ticket.Number = $"SUP-{ticket.Id:D6}";
        db.SupportTicketMessages.Add(new SupportTicketMessage
        {
            TicketId = ticket.Id,
            SenderType = TicketSenderType.User,
            SenderUserId = userId,
            Message = message
        });
        saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;
        return ticket;
    }
}

// ---------- POST /support/tickets ----------

public sealed record CreateSupportTicketCommand(long? SubjectId, string? Subject, string? Message, long? DealId)
    : IRequest<Result<SupportTicketDetailDto>>;

public sealed class CreateSupportTicketValidator : AbstractValidator<CreateSupportTicketCommand>
{
    public CreateSupportTicketValidator()
    {
        RuleFor(x => x.Subject!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.Subject is not null);
        RuleFor(x => x.Message!).MaximumLength(2000).WithErrorCode("TOO_LONG").When(x => x.Message is not null);
    }
}

public sealed class CreateSupportTicketHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<CreateSupportTicketCommand, Result<SupportTicketDetailDto>>
{
    public async Task<Result<SupportTicketDetailDto>> Handle(CreateSupportTicketCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        if (string.IsNullOrWhiteSpace(r.Message)) return Error.Validation("INVALID_VALUE", "message");

        var subject = await SupportTicketCreation.ResolveSubjectAsync(db, r.SubjectId, r.Subject, ct);
        if (subject is null) return Error.Validation("INVALID_VALUE", "subject");

        if (r.DealId is { } dealId)
        {
            var deal = await db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dealId, ct);
            if (deal is null) return DealErrors.NotFound;
            if (deal.BuyerId != userId && deal.SellerId != userId) return DealErrors.NotAllowed;
        }

        var created = await SupportTicketCreation.InsertAsync(
            db, userId, r.SubjectId, subject, r.Message.Trim(), r.DealId, ct);
        if (created.IsFailure) return created.Error;
        return await TicketMapper.DetailAsync(db, created.Value, ct, files);
    }
}

// ---------- POST /support/contact (the contact form creates a ticket) ----------

public sealed record CreateContactMessageCommand(long? SubjectId, string? Subject, string? Message)
    : IRequest<Result<SupportTicketDetailDto>>;

public sealed class CreateContactMessageValidator : AbstractValidator<CreateContactMessageCommand>
{
    public CreateContactMessageValidator()
    {
        RuleFor(x => x.Subject!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.Subject is not null);
        RuleFor(x => x.Message!).MaximumLength(200).WithErrorCode("TOO_LONG").When(x => x.Message is not null);
    }
}

public sealed class CreateContactMessageHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<CreateContactMessageCommand, Result<SupportTicketDetailDto>>
{
    public async Task<Result<SupportTicketDetailDto>> Handle(CreateContactMessageCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        if (string.IsNullOrWhiteSpace(r.Message)) return Error.Validation("INVALID_VALUE", "message");

        var subject = await SupportTicketCreation.ResolveSubjectAsync(db, r.SubjectId, r.Subject, ct);
        if (subject is null) return Error.Validation("INVALID_VALUE", "subject");

        var created = await SupportTicketCreation.InsertAsync(
            db, userId, r.SubjectId, subject, r.Message.Trim(), dealId: null, ct);
        if (created.IsFailure) return created.Error;
        return await TicketMapper.DetailAsync(db, created.Value, ct, files);
    }
}

// ---------- GET /support/tickets ----------

public sealed record GetMySupportTicketsQuery(string? Status, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<SupportTicketSummaryDto>>>;

public sealed class GetMySupportTicketsHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMySupportTicketsQuery, Result<PagedResult<SupportTicketSummaryDto>>>
{
    public async Task<Result<PagedResult<SupportTicketSummaryDto>>> Handle(
        GetMySupportTicketsQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        if (!TicketMapper.TryParseStatusFilter(r.Status, out var statuses))
            return Error.Validation("INVALID_VALUE", "status");

        var page = Math.Max(r.Page, 1);
        var pageSize = Math.Clamp(r.PageSize, 1, 50);

        var query = db.SupportTickets.AsNoTracking().Where(t => t.UserId == userId);
        if (statuses is not null) query = query.Where(t => statuses.Contains(t.Status));

        var total = await query.CountAsync(ct);
        var tickets = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = new List<SupportTicketSummaryDto>();
        foreach (var ticket in tickets)
            items.Add(await TicketMapper.SummaryAsync(db, ticket, ct));

        return new PagedResult<SupportTicketSummaryDto>(items, page, pageSize, total);
    }
}

// ---------- GET /support/tickets/{id} ----------

public sealed record GetSupportTicketDetailQuery(long TicketId)
    : IRequest<Result<SupportTicketDetailDto>>;

public sealed class GetSupportTicketDetailHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<GetSupportTicketDetailQuery, Result<SupportTicketDetailDto>>
{
    public async Task<Result<SupportTicketDetailDto>> Handle(GetSupportTicketDetailQuery r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        var ticket = await db.SupportTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == r.TicketId, ct);
        if (ticket is null || ticket.UserId != userId) return SupportErrors.NotFound;
        return await TicketMapper.DetailAsync(db, ticket, ct, files);
    }
}

// ---------- POST /support/tickets/{id}/messages ----------

public sealed record PostSupportTicketMessageCommand(long TicketId, string? Message, string? FilePublicId)
    : IRequest<Result<SupportTicketMessageDto>>;

public sealed class PostSupportTicketMessageValidator : AbstractValidator<PostSupportTicketMessageCommand>
{
    public PostSupportTicketMessageValidator()
    {
        RuleFor(x => x.Message!).MaximumLength(1000).WithErrorCode("TOO_LONG").When(x => x.Message is not null);
    }
}

public sealed class PostSupportTicketMessageHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver files)
    : IRequestHandler<PostSupportTicketMessageCommand, Result<SupportTicketMessageDto>>
{
    public async Task<Result<SupportTicketMessageDto>> Handle(
        PostSupportTicketMessageCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        if (string.IsNullOrWhiteSpace(r.Message) && string.IsNullOrWhiteSpace(r.FilePublicId))
            return Error.Validation("INVALID_VALUE", "message");

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == r.TicketId, ct);
        if (ticket is null || ticket.UserId != userId) return SupportErrors.NotFound;
        if (ticket.Status == TicketStatus.Closed) return SupportErrors.InvalidState;

        StoredFile? file = null;
        if (!string.IsNullOrWhiteSpace(r.FilePublicId))
        {
            if (!Guid.TryParse(r.FilePublicId.Trim(), out var publicId))
                return Error.Validation("INVALID_VALUE", "filePublicId");
            file = await db.Files.FirstOrDefaultAsync(f => f.PublicId == publicId, ct);
            if (file is null || file.OwnerUserId != userId || file.Purpose != FilePurpose.Support
                || file.Status != FileStatus.Uploaded)
                return Error.Validation("INVALID_VALUE", "filePublicId");
        }

        var message = new SupportTicketMessage
        {
            TicketId = ticket.Id,
            SenderType = TicketSenderType.User,
            SenderUserId = userId,
            Message = string.IsNullOrWhiteSpace(r.Message) ? string.Empty : r.Message.Trim(),
            FileId = file?.Id
        };
        db.SupportTicketMessages.Add(message);
        if (file is not null) file.Status = FileStatus.Attached;

        var saved = await db.SaveGuardedAsync(ct);
        if (saved.IsFailure) return saved.Error;

        var senderName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        return new SupportTicketMessageDto(message.Id, ApiNames.Of(message.SenderType),
            senderName ?? string.Empty, message.Message,
            file is null ? null : files.Resolve(file), message.CreatedAt);
    }
}
