using Etamid.SharedKernel;

namespace Etamid.Domain.Config;

public enum SettingValueType { String, Int, Decimal, Bool, Json }
public enum ShippingIntegrationType { Aramex, Manual }

public sealed class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public SettingValueType ValueType { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? UpdatedByAdminId { get; set; }
}

/// <summary>One generic table for all small fixed lists (reject reasons, dispute reasons, payment methods...).</summary>
public sealed class Lookup : Entity<long>
{
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public long? IconFileId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ShippingCompany : Entity<long>
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public long? LogoFileId { get; set; }
    public ShippingIntegrationType IntegrationType { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Faq : Entity<long>
{
    public string QuestionAr { get; set; } = string.Empty;
    public string QuestionEn { get; set; } = string.Empty;
    public string AnswerAr { get; set; } = string.Empty;
    public string AnswerEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
