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
            L(25, "store_category", "other", "أخرى", "Other", 5),
            L(26, "ticket_subject", "payment", "الدفع والتحويلات", "Payments and transfers", 1),
            L(27, "ticket_subject", "deal", "صفقة", "A deal", 2),
            L(28, "ticket_subject", "account", "الحساب والتحقق", "Account and verification", 3),
            L(29, "ticket_subject", "technical", "مشكلة تقنية", "Technical issue", 4),
            L(30, "ticket_subject", "other", "موضوع آخر", "Other", 5));
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

        // FAQ content matches the mobile FAQ screen (6 questions). Draft wording until
        // the client confirms the final texts; edit here and in etamid-faqs-seed.sql together.
        b.HasData(
            new { Id = 1L, QuestionAr = "ما هي منصة اعتمد؟", QuestionEn = "What is Etamid?", AnswerAr = "منصة اعتمد هي منصة وساطة مالية تحفظ أموال الصفقة بين المشتري والبائع حتى يتم التسليم والموافقة على المنتج.", AnswerEn = "Etamid is a financial escrow platform that holds the deal amount between the buyer and the seller until the product is delivered and approved.", SortOrder = 1, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 2L, QuestionAr = "كيف يمكنني إنشاء حساب جديد؟", QuestionEn = "How do I create a new account?", AnswerAr = "سجّل برقم جوالك وكلمة مرور، ثم أدخل رمز التحقق المرسل إلى بريدك الإلكتروني. بعدها يمكنك إكمال بيانات حسابك، وإذا كنت بائعاً أنشئ متجرك من صفحة حسابي.", AnswerEn = "Register with your phone number and a password, then enter the verification code sent to your email. After that you can complete your profile, and if you are a seller, create your store from your account page.", SortOrder = 2, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 3L, QuestionAr = "كيف يمكنني الدفع في المنصة؟", QuestionEn = "How do I pay on the platform?", AnswerAr = "ادفع من صفحة الصفقة بعد قبولها؛ يتم حجز المبلغ في حساب الضمان ولا يصل إلى البائع إلا بعد استلامك المنتج وموافقتك عليه.", AnswerEn = "Pay from the deal page after it is accepted; the amount is held in the escrow account and reaches the seller only after you receive the product and approve it.", SortOrder = 3, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 4L, QuestionAr = "هل بياناتي الشخصية والمالية آمنة؟", QuestionEn = "Is my personal and financial data safe?", AnswerAr = "نعم، بياناتك محمية ولا تُشارك مع الطرف الآخر إلا ما يلزم لإتمام الصفقة، وأموال الصفقات محفوظة في حساب الضمان.", AnswerEn = "Yes, your data is protected and is only shared with the other party as needed to complete the deal, and deal funds are held in the escrow account.", SortOrder = 4, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 5L, QuestionAr = "هل توجد رسوم لاستخدام المنصة؟", QuestionEn = "Are there fees for using the platform?", AnswerAr = "رسوم المنصة ثابتة قدرها 50 ريال على البائع لكل صفقة، ولا توجد رسوم على المشتري.", AnswerEn = "The platform fee is a fixed 50 SAR per deal, paid by the seller. There is no fee for the buyer.", SortOrder = 5, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new { Id = 6L, QuestionAr = "كيف يمكنني متابعة طلباتي؟", QuestionEn = "How do I track my requests?", AnswerAr = "من صفحة صفقاتي تتابع حالة كل صفقة خطوة بخطوة، ومن صفحة طلبات الدعم تتابع ردود فريق الدعم على طلباتك.", AnswerEn = "From the My Deals page you can follow each deal step by step, and from the Support Requests page you can follow the support team's replies to your requests.", SortOrder = 6, IsActive = true, UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
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
