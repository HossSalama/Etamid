using Etamid.Domain.Admin;
using Etamid.Domain.Config;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

public sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> b)
    {
        b.Property(x => x.Number).HasMaxLength(20);
        b.Property(x => x.Details).HasMaxLength(200);
        b.Property(x => x.ResolutionNote).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Number).IsUnique();

        // One unresolved dispute per deal.
        b.HasIndex(x => x.DealId).IsUnique().HasFilter("[Status] IN ('UnderReview','AwaitingInfo')");

        b.ForeignKey<Dispute, Deal>(x => x.DealId);
        b.ForeignKey<Dispute, User>(x => x.OpenedById);
        b.ForeignKey<Dispute, User>(x => x.AgainstUserId);
        b.ForeignKey<Dispute, Lookup>(x => x.ReasonId);
        b.ForeignKey<Dispute, AdminUser>(x => x.ResolvedByAdminId);
        b.ForeignKey<Dispute, AdminUser>(x => x.AssignedAdminId);
    }
}

public sealed class DisputeMessageConfiguration : IEntityTypeConfiguration<DisputeMessage>
{
    public void Configure(EntityTypeBuilder<DisputeMessage> b)
    {
        b.Property(x => x.Message).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.DisputeId, x.CreatedAt });
        b.ForeignKey<DisputeMessage, Dispute>(x => x.DisputeId);
        b.ForeignKey<DisputeMessage, User>(x => x.SenderUserId);
        b.ForeignKey<DisputeMessage, AdminUser>(x => x.SenderAdminId);
    }
}

public sealed class DisputeAttachmentConfiguration : IEntityTypeConfiguration<DisputeAttachment>
{
    public void Configure(EntityTypeBuilder<DisputeAttachment> b)
    {
        b.HasIndex(x => x.DisputeId);
        b.ForeignKey<DisputeAttachment, Dispute>(x => x.DisputeId);
        b.ForeignKey<DisputeAttachment, DisputeMessage>(x => x.MessageId);
        b.ForeignKey<DisputeAttachment, StoredFile>(x => x.FileId);
        b.ForeignKey<DisputeAttachment, User>(x => x.UploadedByUserId);
    }
}

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> b)
    {
        b.Property(x => x.Number).HasMaxLength(20);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.Message).HasColumnType("nvarchar(max)");
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.ForeignKey<SupportTicket, User>(x => x.UserId);
        b.ForeignKey<SupportTicket, Deal>(x => x.DealId);
        b.ForeignKey<SupportTicket, Lookup>(x => x.SubjectId);
        b.ForeignKey<SupportTicket, AdminUser>(x => x.AssignedAdminId);
    }
}

public sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> b)
    {
        b.Property(x => x.Message).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.TicketId, x.CreatedAt });
        b.ForeignKey<SupportTicketMessage, SupportTicket>(x => x.TicketId);
        b.ForeignKey<SupportTicketMessage, User>(x => x.SenderUserId);
        b.ForeignKey<SupportTicketMessage, AdminUser>(x => x.SenderAdminId);
        b.ForeignKey<SupportTicketMessage, StoredFile>(x => x.FileId);
    }
}
