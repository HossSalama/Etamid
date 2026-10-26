using System.Reflection;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Etamid.Domain.Common;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Config;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.Domain.Money;
using Etamid.Domain.Notifications;
using Etamid.Domain.Support;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    // A: Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<StoreProfile> StoreProfiles => Set<StoreProfile>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<NafathVerification> NafathVerifications => Set<NafathVerification>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<TermsVersion> TermsVersions => Set<TermsVersion>();
    public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();

    // B: Config, C: Files
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Lookup> Lookups => Set<Lookup>();
    public DbSet<ShippingCompany> ShippingCompanies => Set<ShippingCompany>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<StoredFile> Files => Set<StoredFile>();

    // D: Deals
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DealEditRequest> DealEditRequests => Set<DealEditRequest>();
    public DbSet<DealStatusHistory> DealStatusHistory => Set<DealStatusHistory>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<Rating> Ratings => Set<Rating>();

    // E: Money
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<EscrowHold> EscrowHolds => Set<EscrowHold>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    // F: Disputes and support
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeMessage> DisputeMessages => Set<DisputeMessage>();
    public DbSet<DisputeAttachment> DisputeAttachments => Set<DisputeAttachment>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();

    // G: Company deals
    public DbSet<CompanyDeal> CompanyDeals => Set<CompanyDeal>();
    public DbSet<CompanyDealStatusHistory> CompanyDealStatusHistory => Set<CompanyDealStatusHistory>();
    public DbSet<PaymentInstallment> PaymentInstallments => Set<PaymentInstallment>();
    public DbSet<InstallmentApproval> InstallmentApprovals => Set<InstallmentApproval>();
    public DbSet<PenaltyCharge> PenaltyCharges => Set<PenaltyCharge>();

    // H: Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<BroadcastCampaign> BroadcastCampaigns => Set<BroadcastCampaign>();

    // I: Admin and system
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
    public DbSet<AdminPermission> AdminPermissions => Set<AdminPermission>();
    public DbSet<AdminRolePermission> AdminRolePermissions => Set<AdminRolePermission>();
    public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<string>().HaveMaxLength(255);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("DealNumbers").StartsAt(45872).IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Enums are stored as readable strings (e.g. "PendingSeller").
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!type.IsEnum) continue;
                property.SetProviderClrType(typeof(string));
                property.SetMaxLength(40);
            }
        }

        // Soft-deleted rows disappear from every query automatically.
        var filter = typeof(ApplicationDbContext)
            .GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (var clrType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType))
                     .Select(t => t.ClrType)
                     .ToList())
        {
            filter.MakeGenericMethod(clrType).Invoke(null, [modelBuilder]);
        }

        base.OnModelCreating(modelBuilder);
    }

    private static void ApplySoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : class, ISoftDeletable
        => modelBuilder.Entity<T>().HasQueryFilter(e => e.DeletedAt == null);
}
