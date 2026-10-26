using Etamid.Domain.Config;
using Etamid.Domain.Files;
using Etamid.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100);
        b.Property(x => x.Value).HasMaxLength(1000);
        b.Property(x => x.Description).HasMaxLength(300);

        var at = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);
        b.HasData(
            new { Key = "min_version_android", Value = "1.0.0", ValueType = SettingValueType.String, Description = "Minimum supported Android app version", UpdatedAt = at },
            new { Key = "min_version_ios", Value = "1.0.0", ValueType = SettingValueType.String, Description = "Minimum supported iOS app version", UpdatedAt = at },
            new { Key = "force_update", Value = "false", ValueType = SettingValueType.Bool, Description = "Block old app versions", UpdatedAt = at },
            new { Key = "support_whatsapp", Value = "966500000000", ValueType = SettingValueType.String, Description = "Support WhatsApp number (placeholder)", UpdatedAt = at },
            new { Key = "platform_fee_amount", Value = "50", ValueType = SettingValueType.Decimal, Description = "Fixed platform fee per deal, in SAR", UpdatedAt = at },
            new { Key = "platform_fee_paid_by", Value = "seller", ValueType = SettingValueType.String, Description = "Who pays the platform fee", UpdatedAt = at },
            new { Key = "currency", Value = "SAR", ValueType = SettingValueType.String, Description = "Default currency", UpdatedAt = at });
    }
}

public sealed class LookupConfiguration : IEntityTypeConfiguration<Lookup>
{
    public void Configure(EntityTypeBuilder<Lookup> b)
    {
        b.Property(x => x.Type).HasMaxLength(40);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.NameAr).HasMaxLength(150);
        b.Property(x => x.NameEn).HasMaxLength(150);
        b.HasIndex(x => new { x.Type, x.Code }).IsUnique();
        b.ForeignKey<Lookup, StoredFile>(x => x.IconFileId);

        // Texts are placeholders until the client confirms the final wording.
        static object L(long id, string type, string code, string ar, string en, int sort)
            => new { Id = id, Type = type, Code = code, NameAr = ar, NameEn = en, SortOrder = sort, IsActive = true };

        b.HasData(
            L(1, "reject_reason", "product_mismatch", "المنتج غير مطابق للوصف", "Product does not match the description", 1),
            L(2, "reject_reason", "price_change", "تغيير في السعر", "Price change", 2),
            L(3, "reject_reason", "unavailable", "المنتج غير متوفر حالياً", "Product is not available", 3),
            L(4, "reject_reason", "buyer_communication", "مشكلة في التواصل مع المشتري", "Communication problem with the buyer", 4),
            L(5, "reject_reason", "spec_mismatch", "المنتج لا يطابق المواصفات", "Product does not match the specs", 5),
            L(6, "reject_reason", "other", "سبب آخر", "Other reason", 6),
            L(7, "dispute_reason", "not_received", "لم أستلم المنتج", "I did not receive the product", 1),
            L(8, "dispute_reason", "not_as_described", "المنتج مختلف عن الوصف المتفق عليه", "Product differs from the agreed description", 2),
            L(9, "dispute_reason", "damaged", "المنتج تالف أو معيب", "Product is damaged or faulty", 3),
            L(10, "dispute_reason", "wrong_item", "استلمت منتجاً خاطئاً", "I received the wrong product", 4),
            L(11, "dispute_reason", "seller_breach", "البائع لم يلتزم بالاتفاق", "The seller did not comply with the agreement", 5),
            L(12, "dispute_reason", "other", "سبب آخر", "Other reason", 6),
            L(13, "company_reject_reason", "terms_not_acceptable", "الشروط غير مقبولة", "Terms not acceptable", 1),
            L(14, "company_reject_reason", "schedule_not_acceptable", "جدول الدفعات غير مقبول", "Payment schedule not acceptable", 2),
            L(15, "company_reject_reason", "penalty_not_acceptable", "الشرط الجزائي غير مقبول", "Penalty clause not acceptable", 3),
            L(16, "company_reject_reason", "wrong_details", "بيانات الشركة غير صحيحة", "Incorrect company details", 4),
            L(17, "company_reject_reason", "other", "سبب آخر", "Other reason", 5),
            L(18, "duration_unit", "hour", "ساعة", "Hour", 1),
            L(19, "duration_unit", "day", "يوم", "Day", 2),
            L(20, "payment_method", "bank_transfer", "تحويل بنكي", "Bank transfer", 1),
            L(21, "store_category", "electronics", "أجهزة إلكترونية", "Electronics", 1),
            L(22, "store_category", "fashion", "أزياء وإكسسوارات", "Fashion and accessories", 2),
            L(23, "store_category", "home", "المنزل والأثاث", "Home and furniture", 3),
            L(24, "store_category", "vehicles", "مركبات وقطع غيار", "Vehicles and parts", 4),
            L(25, "store_category", "other", "أخرى", "Other", 5));
    }
}

public sealed class ShippingCompanyConfiguration : IEntityTypeConfiguration<ShippingCompany>
{
    public void Configure(EntityTypeBuilder<ShippingCompany> b)
    {
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.NameAr).HasMaxLength(150);
        b.Property(x => x.NameEn).HasMaxLength(150);
        b.HasIndex(x => x.Code).IsUnique();
        b.ForeignKey<ShippingCompany, StoredFile>(x => x.LogoFileId);

        b.HasData(new
        {
            Id = 1L, Code = "aramex", NameAr = "أرامكس", NameEn = "Aramex",
            IntegrationType = ShippingIntegrationType.Aramex, IsActive = true, SortOrder = 1
        });
    }
}

public sealed class FaqConfiguration : IEntityTypeConfiguration<Faq>
{
    public void Configure(EntityTypeBuilder<Faq> b)
    {
        b.Property(x => x.QuestionAr).HasMaxLength(300);
        b.Property(x => x.QuestionEn).HasMaxLength(300);
        b.Property(x => x.AnswerAr).HasColumnType("nvarchar(max)");
        b.Property(x => x.AnswerEn).HasColumnType("nvarchar(max)");

        // Placeholder content until the client supplies the final FAQ texts.
        b.HasData(
            new { Id = 1L, QuestionAr = "ما هي منصة اعتمد؟", QuestionEn = "What is Etamid?", AnswerAr = "منصة وساطة مالية تحفظ أموال الصفقة بين المشتري والبائع حتى يتم التسليم والموافقة.", AnswerEn = "A financial escrow platform that holds the deal amount between buyer and seller until delivery is confirmed.", SortOrder = 1, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 2L, QuestionAr = "كم رسوم المنصة؟", QuestionEn = "What are the platform fees?", AnswerAr = "رسوم ثابتة قدرها 50 ريال على البائع لكل صفقة.", AnswerEn = "A fixed fee of 50 SAR per deal, paid by the seller.", SortOrder = 2, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 3L, QuestionAr = "كيف أدفع قيمة الصفقة؟", QuestionEn = "How do I pay for a deal?", AnswerAr = "الدفع عن طريق التحويل البنكي إلى حساب المنصة وسيتم تأكيده بعد التحقق.", AnswerEn = "Payment is by bank transfer to the platform account and is confirmed after verification.", SortOrder = 3, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 4L, QuestionAr = "ماذا أفعل إذا لم أستلم المنتج؟", QuestionEn = "What if I do not receive the product?", AnswerAr = "يمكنك فتح نزاع من صفحة الصفقة وسيتم تجميد المبلغ حتى صدور القرار.", AnswerEn = "You can open a dispute from the deal page and the amount stays frozen until a decision is made.", SortOrder = 4, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
    }
}

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.Property(x => x.StorageKey).HasMaxLength(300);
        b.Property(x => x.FileName).HasMaxLength(200);
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.HasIndex(x => x.PublicId).IsUnique();
        b.ForeignKey<StoredFile, User>(x => x.OwnerUserId);
    }
}
