using Etamid.Domain.Admin;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Deals;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Etamid.Domain.Money;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.UserId).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_Wallets_Balance", "[AvailableBalance] >= 0"));
        b.ForeignKey<Wallet, User>(x => x.UserId);
        b.ForeignKey<Wallet, LedgerAccount>(x => x.LedgerAccountId);
    }
}

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> b)
    {
        b.Property(x => x.Currency).HasMaxLength(3);
        b.HasIndex(x => x.OwnerUserId);
        b.ForeignKey<LedgerAccount, User>(x => x.OwnerUserId);
    }
}

public sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> b)
    {
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.HasIndex(x => x.PublicId).IsUnique();
        b.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.ForeignKey<LedgerTransaction, Deal>(x => x.DealId);
        b.ForeignKey<LedgerTransaction, CompanyDeal>(x => x.CompanyDealId);
        b.ForeignKey<LedgerTransaction, PaymentInstallment>(x => x.InstallmentId);
        b.ForeignKey<LedgerTransaction, WithdrawalRequest>(x => x.WithdrawalId);
        b.ForeignKey<LedgerTransaction, Payment>(x => x.PaymentId);
    }
}

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.HasIndex(x => new { x.AccountId, x.CreatedAt });
        b.ToTable(t => t.HasCheckConstraint("CK_LedgerEntries_Amount", "[Amount] > 0"));
        b.ForeignKey<LedgerEntry, LedgerTransaction>(x => x.TransactionId);
        b.ForeignKey<LedgerEntry, LedgerAccount>(x => x.AccountId);
    }
}

public sealed class EscrowHoldConfiguration : IEntityTypeConfiguration<EscrowHold>
{
    public void Configure(EntityTypeBuilder<EscrowHold> b)
    {
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.DealId).IsUnique();
        b.ForeignKey<EscrowHold, Deal>(x => x.DealId);
        b.ForeignKey<EscrowHold, User>(x => x.BuyerId);
        b.ForeignKey<EscrowHold, User>(x => x.SellerId);
        b.ForeignKey<EscrowHold, Payment>(x => x.PaymentId);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Provider).HasMaxLength(30);
        b.Property(x => x.ProviderPaymentId).HasMaxLength(100);
        b.Property(x => x.ProviderPayload).HasColumnType("nvarchar(max)");
        b.Property(x => x.FailureCode).HasMaxLength(50);
        b.Property(x => x.FailureMessage).HasMaxLength(300);
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.Property(x => x.TransferReference).HasMaxLength(60);
        b.HasIndex(x => x.PublicId).IsUnique();
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => x.ProviderPaymentId).IsUnique().HasFilter("[ProviderPaymentId] IS NOT NULL");
        b.ForeignKey<Payment, User>(x => x.UserId);
        b.ForeignKey<Payment, Deal>(x => x.DealId);
        b.ForeignKey<Payment, PaymentInstallment>(x => x.InstallmentId);
        b.ForeignKey<Payment, StoredFile>(x => x.ProofFileId);
        b.ForeignKey<Payment, AdminUser>(x => x.ConfirmedByAdminId);
    }
}

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> b)
    {
        b.Property(x => x.Provider).HasMaxLength(30);
        b.Property(x => x.ProviderEventId).HasMaxLength(100);
        b.Property(x => x.EventType).HasMaxLength(60);
        b.Property(x => x.Payload).HasColumnType("nvarchar(max)");
        b.Property(x => x.Error).HasMaxLength(500);
        b.HasIndex(x => new { x.Provider, x.ProviderEventId }).IsUnique();
    }
}

public sealed class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> b)
    {
        b.Property(x => x.IbanSnapshotMasked).HasMaxLength(34);
        b.Property(x => x.RejectionReason).HasMaxLength(300);
        b.Property(x => x.BankReference).HasMaxLength(60);
        b.Property(x => x.IdempotencyKey).HasMaxLength(64);
        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.ForeignKey<WithdrawalRequest, User>(x => x.UserId);
        b.ForeignKey<WithdrawalRequest, Wallet>(x => x.WalletId);
        b.ForeignKey<WithdrawalRequest, BankAccount>(x => x.BankAccountId);
        b.ForeignKey<WithdrawalRequest, AdminUser>(x => x.ApprovedByAdminId);
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.HasKey(x => new { x.UserId, x.Key });
        b.Property(x => x.Key).HasMaxLength(64);
        b.Property(x => x.RequestHash).HasMaxLength(64);
        b.Property(x => x.Endpoint).HasMaxLength(100);
        b.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
        b.HasIndex(x => x.ExpiresAt);
    }
}
