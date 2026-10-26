using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Support;

public enum TicketStatus { Open, InReview, Closed }
public enum TicketSenderType { User, Admin }

public sealed class SupportTicket : AuditableEntity<long>
{
    public string Number { get; set; } = string.Empty;
    public long UserId { get; set; }
    public long? DealId { get; set; }
    public long? SubjectId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public long? AssignedAdminId { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public sealed class SupportTicketMessage : CreatedEntity<long>
{
    public long TicketId { get; set; }
    public TicketSenderType SenderType { get; set; }
    public long? SenderUserId { get; set; }
    public long? SenderAdminId { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? FileId { get; set; }
}
