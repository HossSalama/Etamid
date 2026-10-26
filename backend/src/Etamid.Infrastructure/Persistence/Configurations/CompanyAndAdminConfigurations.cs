using Etamid.Domain.Admin;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Config;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.Domain.Money;
using Etamid.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

public sealed class CompanyDealConfiguration : IEntityTypeConfiguration<CompanyDeal>
{
    public void Configure(EntityTypeBuilder<CompanyDeal> b)
    {
        b.Property(x => x.Number).HasDefaultValueSql("NEXT VALUE FOR DealNumbers");
        b.Property(x => x.CounterpartyName).HasMaxLength(200);
        b.Property(x => x.CounterpartyPhone).HasMaxLength(20);
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Details).HasMaxLength(200);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.ApprovalNotes).HasMaxLength(200);
        b.Property(x => x.PenaltyPercent).HasPrecision(5, 2);
        b.Property(x => x.PenaltyClauses).HasMaxLength(200);
        b.Property(x => x.RejectNote).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => new { x.CreatorUserId, x.Status });
        b.HasIndex(x => new { x.CounterpartyUserId, x.Status });
        b.ForeignKey<CompanyDeal, User>(x => x.CreatorUserId);
        b.ForeignKey<CompanyDeal, User>(x => x.CounterpartyUserId);
        b.ForeignKey<CompanyDeal, StoredFile>(x => x.ContractFileId);
        b.ForeignKey<CompanyDeal, Lookup>(x => x.RejectReasonId);
    }
}

public sealed class CompanyDealStatusHistoryConfiguration : IEntityTypeConfiguration<CompanyDealStatusHistory>
{
    public void Configure(EntityTypeBuilder<CompanyDealStatusHistory> b)
    {
        b.Property(x => x.FromStatus).HasMaxLength(30);
        b.Property(x => x.ToStatus).HasMaxLength(30);
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasIndex(x => new { x.CompanyDealId, x.CreatedAt });
        b.ForeignKey<CompanyDealStatusHistory, CompanyDeal>(x => x.CompanyDealId);
        b.ForeignKey<CompanyDealStatusHistory, User>(x => x.ActorUserId);
    }
}

public sealed class PaymentInstallmentConfiguration : IEntityTypeConfiguration<PaymentInstallment>
{
    public void Configure(EntityTypeBuilder<PaymentInstallment> b)
    {
        b.Property(x => x.Label).HasMaxLength(50);
        b.Property(x => x.Notes).HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.CompanyDealId, x.Order }).IsUnique();
        b.HasIndex(x => new { x.Status, x.DueDate });
        b.ToTable(t => t.HasCheckConstraint("CK_PaymentInstallments_Amount", "[Amount] > 0"));
        b.ForeignKey<PaymentInstallment, CompanyDeal>(x => x.CompanyDealId);
        b.ForeignKey<PaymentInstallment, Payment>(x => x.PaymentId);
    }
}

public sealed class InstallmentApprovalConfiguration : IEntityTypeConfiguration<InstallmentApproval>
{
    public void Configure(EntityTypeBuilder<InstallmentApproval> b)
    {
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasIndex(x => x.InstallmentId);
        b.ForeignKey<InstallmentApproval, PaymentInstallment>(x => x.InstallmentId);
        b.ForeignKey<InstallmentApproval, User>(x => x.DecidedByUserId);
    }
}

public sealed class PenaltyChargeConfiguration : IEntityTypeConfiguration<PenaltyCharge>
{
    public void Configure(EntityTypeBuilder<PenaltyCharge> b)
    {
        b.Property(x => x.PenaltyPercent).HasPrecision(5, 2);
        b.Property(x => x.WaiverReason).HasMaxLength(300);
        b.HasIndex(x => x.InstallmentId).IsUnique();   // one penalty per installment (client to confirm)
        b.ForeignKey<PenaltyCharge, PaymentInstallment>(x => x.InstallmentId);
        b.ForeignKey<PenaltyCharge, CompanyDeal>(x => x.CompanyDealId);
        b.ForeignKey<PenaltyCharge, LedgerTransaction>(x => x.LedgerTransactionId);
        b.ForeignKey<PenaltyCharge, AdminUser>(x => x.WaivedByAdminId);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.TitleAr).HasMaxLength(150);
        b.Property(x => x.TitleEn).HasMaxLength(150);
        b.Property(x => x.BodyAr).HasMaxLength(500);
        b.Property(x => x.BodyEn).HasMaxLength(500);
        b.Property(x => x.Data).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        b.ForeignKey<Notification, User>(x => x.UserId);
    }
}

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> b)
    {
        b.Property(x => x.Code).HasMaxLength(60);
        b.Property(x => x.TitleAr).HasMaxLength(150);
        b.Property(x => x.TitleEn).HasMaxLength(150);
        b.Property(x => x.BodyAr).HasMaxLength(500);
        b.Property(x => x.BodyEn).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class BroadcastCampaignConfiguration : IEntityTypeConfiguration<BroadcastCampaign>
{
    public void Configure(EntityTypeBuilder<BroadcastCampaign> b)
    {
        b.Property(x => x.TitleAr).HasMaxLength(150);
        b.Property(x => x.TitleEn).HasMaxLength(150);
        b.Property(x => x.BodyAr).HasMaxLength(500);
        b.Property(x => x.BodyEn).HasMaxLength(500);
        b.Property(x => x.AudienceFilter).HasColumnType("nvarchar(max)");
        b.ForeignKey<BroadcastCampaign, AdminUser>(x => x.CreatedByAdminId);
    }
}

public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> b)
    {
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.Email).HasMaxLength(190);
        b.Property(x => x.PasswordHash).HasMaxLength(255);
        b.Property(x => x.TwoFactorSecret).HasMaxLength(255);
        b.HasIndex(x => x.Email).IsUnique();
    }
}

public sealed class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class AdminPermissionConfiguration : IEntityTypeConfiguration<AdminPermission>
{
    public void Configure(EntityTypeBuilder<AdminPermission> b)
    {
        b.Property(x => x.Code).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class AdminRolePermissionConfiguration : IEntityTypeConfiguration<AdminRolePermission>
{
    public void Configure(EntityTypeBuilder<AdminRolePermission> b)
    {
        b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.ForeignKey<AdminRolePermission, AdminRole>(x => x.RoleId);
        b.ForeignKey<AdminRolePermission, AdminPermission>(x => x.PermissionId);
    }
}

public sealed class AdminUserRoleConfiguration : IEntityTypeConfiguration<AdminUserRole>
{
    public void Configure(EntityTypeBuilder<AdminUserRole> b)
    {
        b.HasKey(x => new { x.AdminUserId, x.RoleId });
        b.ForeignKey<AdminUserRole, AdminUser>(x => x.AdminUserId);
        b.ForeignKey<AdminUserRole, AdminRole>(x => x.RoleId);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Action).HasMaxLength(80);
        b.Property(x => x.EntityType).HasMaxLength(50);
        b.Property(x => x.OldValues).HasColumnType("nvarchar(max)");
        b.Property(x => x.NewValues).HasColumnType("nvarchar(max)");
        b.Property(x => x.Ip).HasMaxLength(45);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => new { x.ActorType, x.ActorId, x.CreatedAt });
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.Property(x => x.Type).HasMaxLength(80);
        b.Property(x => x.Payload).HasColumnType("nvarchar(max)");
        b.Property(x => x.Error).HasMaxLength(500);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}
