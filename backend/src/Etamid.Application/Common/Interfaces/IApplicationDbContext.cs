using Etamid.Domain.Admin;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Config;
using Etamid.Domain.Deals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.Domain.Money;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Common.Interfaces;

/// <summary>
/// Persistence abstraction used by handlers. The sets most use cases need are exposed directly;
/// any other table is reachable through <see cref="Set{TEntity}"/>.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<CompanyProfile> CompanyProfiles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Device> Devices { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<Bank> Banks { get; }
    DbSet<TermsVersion> TermsVersions { get; }
    DbSet<TermsAcceptance> TermsAcceptances { get; }
    DbSet<AuthAuditLog> AuthAuditLogs { get; }
    DbSet<EmailOtp> EmailOtps { get; }
    DbSet<StoredFile> Files { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<WithdrawalRequest> WithdrawalRequests { get; }
    DbSet<AppSetting> AppSettings { get; }
    DbSet<Lookup> Lookups { get; }
    DbSet<ShippingCompany> ShippingCompanies { get; }
    DbSet<Faq> Faqs { get; }
    DbSet<StoreProfile> StoreProfiles { get; }
    DbSet<Product> Products { get; }
    DbSet<Address> Addresses { get; }
    DbSet<NafathVerification> NafathVerifications { get; }

    // Deals and escrow
    DbSet<Deal> Deals { get; }
    DbSet<DealEditRequest> DealEditRequests { get; }
    DbSet<DealStatusHistory> DealStatusHistory { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; }
    DbSet<LedgerAccount> LedgerAccounts { get; }
    DbSet<LedgerTransaction> LedgerTransactions { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<EscrowHold> EscrowHolds { get; }
    DbSet<IdempotencyRecord> IdempotencyKeys { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<DisputeMessage> DisputeMessages { get; }
    DbSet<DisputeAttachment> DisputeAttachments { get; }
    DbSet<DisputeSettlementProposal> DisputeSettlementProposals { get; }
    DbSet<Rating> Ratings { get; }
    // The Shipments table already exists (Initial migration); only the interface
    // exposure was missing, so no new migration is needed for tracking.
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentEvent> ShipmentEvents { get; }

    // Company deals: the tables already exist (Initial migration); only the
    // interface exposure was missing, so no new migration is needed.
    DbSet<CompanyDeal> CompanyDeals { get; }
    DbSet<CompanyDealStatusHistory> CompanyDealStatusHistory { get; }
    DbSet<PaymentInstallment> PaymentInstallments { get; }
    DbSet<InstallmentApproval> InstallmentApprovals { get; }
    DbSet<PenaltyCharge> PenaltyCharges { get; }

    // Admin (Cycle 9): the tables and EF configs already existed; only the exposure was missing.
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<AdminRole> AdminRoles { get; }
    DbSet<AdminPermission> AdminPermissions { get; }
    DbSet<AdminRolePermission> AdminRolePermissions { get; }
    DbSet<AdminUserRole> AdminUserRoles { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<AdminRefreshToken> AdminRefreshTokens { get; }
    DbSet<AdminTwoFactorChallenge> AdminTwoFactorChallenges { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
