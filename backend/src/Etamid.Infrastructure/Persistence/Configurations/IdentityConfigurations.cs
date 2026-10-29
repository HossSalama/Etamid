using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Config;
using Etamid.Domain.Deals;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.Code).HasMaxLength(12);
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.PhoneNormalized).HasMaxLength(15);
        b.Property(x => x.Email).HasMaxLength(190);
        b.Property(x => x.PasswordHash).HasMaxLength(255);

        // Unique among non-deleted users, so a deleted account's phone/email can be reused.
        b.HasIndex(x => x.PhoneNormalized).IsUnique().HasFilter("[DeletedAt] IS NULL");
        b.HasIndex(x => x.Email).IsUnique().HasFilter("[DeletedAt] IS NULL");
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[DeletedAt] IS NULL");
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.CompanyProfile).WithOne().HasForeignKey<CompanyProfile>(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        b.ForeignKey<User, StoredFile>(x => x.AvatarFileId);
    }
}

public sealed class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.CompanyName).HasMaxLength(200);
        b.Property(x => x.OwnerPhone).HasMaxLength(20);
        b.Property(x => x.CommercialRegNo).HasMaxLength(30);
        b.Property(x => x.TaxNo).HasMaxLength(30);
        b.HasIndex(x => x.CommercialRegNo).IsUnique();
    }
}

public sealed class StoreProfileConfiguration : IEntityTypeConfiguration<StoreProfile>
{
    public void Configure(EntityTypeBuilder<StoreProfile> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.StoreName).HasMaxLength(150);
        b.Property(x => x.RatingAvg).HasPrecision(3, 2);
        b.ForeignKey<StoreProfile, User>(x => x.UserId);
        b.ForeignKey<StoreProfile, Lookup>(x => x.CategoryId);
        b.ForeignKey<StoreProfile, StoredFile>(x => x.BannerFileId);
    }
}

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.Property(x => x.Label).HasMaxLength(50);
        b.Property(x => x.RecipientName).HasMaxLength(150);
        b.Property(x => x.RecipientPhone).HasMaxLength(20);
        b.Property(x => x.City).HasMaxLength(80);
        b.Property(x => x.District).HasMaxLength(80);
        b.Property(x => x.Street).HasMaxLength(150);
        b.Property(x => x.BuildingNo).HasMaxLength(20);
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.AdditionalNo).HasMaxLength(10);
        b.Property(x => x.FullText).HasMaxLength(300);
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.HasIndex(x => x.UserId);
        b.ForeignKey<Address, User>(x => x.UserId);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(255);
        b.Property(x => x.CreatedIp).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(300);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.FamilyId);
        b.ForeignKey<RefreshToken, User>(x => x.UserId);
        b.ForeignKey<RefreshToken, Device>(x => x.DeviceId);
    }
}

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.Property(x => x.DeviceToken).HasMaxLength(500);
        b.Property(x => x.AppVersion).HasMaxLength(20);
        b.Property(x => x.OsVersion).HasMaxLength(20);
        b.HasIndex(x => x.DeviceToken).IsUnique();
        b.HasIndex(x => x.UserId);
        b.ForeignKey<Device, User>(x => x.UserId);
    }
}

public sealed class NafathVerificationConfiguration : IEntityTypeConfiguration<NafathVerification>
{
    public void Configure(EntityTypeBuilder<NafathVerification> b)
    {
        b.Property(x => x.RequestId).HasMaxLength(100);
        b.Property(x => x.RandomNumber).HasMaxLength(4);
        b.Property(x => x.NationalIdHash).HasMaxLength(255);
        b.Property(x => x.VerifiedName).HasMaxLength(150);
        b.HasIndex(x => x.RequestId).IsUnique();
        b.ForeignKey<NafathVerification, User>(x => x.UserId);
    }
}

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> b)
    {
        b.Property(x => x.Code).HasMaxLength(4);
        b.Property(x => x.NameAr).HasMaxLength(150);
        b.Property(x => x.NameEn).HasMaxLength(150);
        b.HasIndex(x => x.Code).IsUnique();
        b.ForeignKey<Bank, StoredFile>(x => x.LogoFileId);

        // Codes are the 2 digits after "SA" + 2 check digits inside a Saudi IBAN. Verify before launch.
        b.HasData(
            new { Id = 1L, Code = "80", NameAr = "مصرف الراجحي", NameEn = "Al Rajhi Bank", IsActive = true },
            new { Id = 2L, Code = "10", NameAr = "البنك الأهلي السعودي", NameEn = "Saudi National Bank", IsActive = true },
            new { Id = 3L, Code = "20", NameAr = "بنك الرياض", NameEn = "Riyad Bank", IsActive = true },
            new { Id = 4L, Code = "45", NameAr = "البنك السعودي الأول", NameEn = "Saudi Awwal Bank", IsActive = true },
            new { Id = 5L, Code = "55", NameAr = "البنك السعودي الفرنسي", NameEn = "Banque Saudi Fransi", IsActive = true },
            new { Id = 6L, Code = "60", NameAr = "بنك الجزيرة", NameEn = "Bank AlJazira", IsActive = true },
            new { Id = 7L, Code = "05", NameAr = "بنك الإنماء", NameEn = "Alinma Bank", IsActive = true },
            new { Id = 8L, Code = "15", NameAr = "بنك البلاد", NameEn = "Bank Albilad", IsActive = true },
            new { Id = 9L, Code = "30", NameAr = "البنك العربي الوطني", NameEn = "Arab National Bank", IsActive = true },
            new { Id = 10L, Code = "65", NameAr = "البنك السعودي للاستثمار", NameEn = "Saudi Investment Bank", IsActive = true });
    }
}

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> b)
    {
        b.Property(x => x.HolderName).HasMaxLength(150);
        b.Property(x => x.IbanEncrypted).HasMaxLength(500);
        b.Property(x => x.IbanLast4).HasMaxLength(4);
        b.Property(x => x.IbanHash).HasMaxLength(64);
        b.HasIndex(x => x.UserId).IsUnique();   // one account per user (client to confirm)
        b.HasIndex(x => x.IbanHash).IsUnique();
        b.ForeignKey<BankAccount, User>(x => x.UserId);
        b.ForeignKey<BankAccount, Bank>(x => x.BankId);
    }
}

public sealed class TermsVersionConfiguration : IEntityTypeConfiguration<TermsVersion>
{
    public void Configure(EntityTypeBuilder<TermsVersion> b)
    {
        b.Property(x => x.Version).HasMaxLength(10);
        b.Property(x => x.TitleAr).HasMaxLength(200);
        b.Property(x => x.TitleEn).HasMaxLength(200);
        b.Property(x => x.ItemsAr).HasColumnType("nvarchar(max)");
        b.Property(x => x.ItemsEn).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.Type, x.Version }).IsUnique();

        // PLACEHOLDER TEXT: replace with the client's legal text before launch.
        var published = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(
            new
            {
                Id = 1L, Type = TermsType.Platform, Version = "1.0", TitleAr = "شروط وأحكام المنصة", TitleEn = "Platform Terms and Conditions",
                ItemsAr = "[\"يجب تقديم بيانات صحيحة عند التسجيل.\",\"تُحجز الأموال إلى حين انتهاء المعاملة.\",\"يحق للمنصة إيقاف الحسابات المخالفة.\"]",
                ItemsEn = "[\"You must provide accurate information when registering.\",\"Funds are held until the deal ends.\",\"Etamid may suspend accounts that violate these terms.\"]",
                PublishedAt = published, IsActive = true
            },
            new
            {
                Id = 2L, Type = TermsType.Deal, Version = "1.0", TitleAr = "شروط وأحكام المعاملة", TitleEn = "Deal Terms and Conditions",
                ItemsAr = "[\"يُحوَّل المبلغ للبائع بعد تأكيد المشتري للاستلام.\",\"على البائع تسليم المنتج كما هو متفق عليه.\",\"عند وجود مشكلة تُطبَّق سياسة النزاعات.\"]",
                ItemsEn = "[\"The amount is transferred to the seller after the buyer confirms receipt.\",\"The seller must deliver the product as agreed.\",\"If there is a problem, the dispute policy applies.\"]",
                PublishedAt = published, IsActive = true
            },
            new
            {
                Id = 3L, Type = TermsType.Company, Version = "1.0", TitleAr = "شروط وأحكام معاملات الشركات", TitleEn = "Company Deal Terms and Conditions",
                ItemsAr = "[\"تُسدَّد الدفعات وفق الجدول المتفق عليه.\",\"تحتاج كل دفعة إلى اعتماد الطرف الآخر.\",\"يُطبَّق الشرط الجزائي تلقائياً بعد انتهاء مهلة السماح.\"]",
                ItemsEn = "[\"Payments follow the agreed schedule.\",\"Each payment needs the other party's approval.\",\"The penalty clause applies automatically after the grace period.\"]",
                PublishedAt = published, IsActive = true
            });
    }
}

public sealed class TermsAcceptanceConfiguration : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> b)
    {
        b.Property(x => x.Ip).HasMaxLength(45);
        b.HasIndex(x => x.UserId);
        b.ForeignKey<TermsAcceptance, User>(x => x.UserId);
        b.ForeignKey<TermsAcceptance, TermsVersion>(x => x.TermsVersionId);
        b.ForeignKey<TermsAcceptance, Deal>(x => x.DealId);
        b.ForeignKey<TermsAcceptance, CompanyDeal>(x => x.CompanyDealId);
        b.ForeignKey<TermsAcceptance, Device>(x => x.DeviceId);
    }
}

public sealed class AuthAuditLogConfiguration : IEntityTypeConfiguration<AuthAuditLog>
{
    public void Configure(EntityTypeBuilder<AuthAuditLog> b)
    {
        b.Property(x => x.PhoneNormalized).HasMaxLength(15);
        b.Property(x => x.Ip).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(300);
        b.Property(x => x.Details).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new { x.PhoneNormalized, x.CreatedAt });
        b.HasIndex(x => new { x.Ip, x.CreatedAt });
    }
}

public sealed class EmailOtpConfiguration : IEntityTypeConfiguration<EmailOtp>
{
    public void Configure(EntityTypeBuilder<EmailOtp> b)
    {
        b.Property(x => x.CodeHash).HasMaxLength(64);
        b.Property(x => x.ResetTokenHash).HasMaxLength(64);
        b.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt });
        b.HasIndex(x => x.ResetTokenHash).IsUnique().HasFilter("[ResetTokenHash] IS NOT NULL");
        b.ForeignKey<EmailOtp, User>(x => x.UserId);
    }
}
