using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Identity;

public sealed class CompanyProfile
{
    private CompanyProfile() { }

    public CompanyProfile(string companyName, string ownerPhone, string commercialRegNo, string? taxNo)
    {
        CompanyName = companyName.Trim();
        OwnerPhone = ownerPhone;
        CommercialRegNo = commercialRegNo.Trim();
        TaxNo = string.IsNullOrWhiteSpace(taxNo) ? null : taxNo.Trim();
    }

    public void Update(string companyName, string ownerPhone, string commercialRegNo, string? taxNo)
    {
        CompanyName = companyName.Trim();
        OwnerPhone = ownerPhone;
        CommercialRegNo = commercialRegNo.Trim();
        TaxNo = string.IsNullOrWhiteSpace(taxNo) ? null : taxNo.Trim();
    }

    public long UserId { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public string OwnerPhone { get; private set; } = string.Empty;
    public string CommercialRegNo { get; private set; } = string.Empty;
    public string? TaxNo { get; private set; }
}

public sealed class StoreProfile
{
    public long UserId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public long? CategoryId { get; set; }
    public long? BannerFileId { get; set; }
    public decimal RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public int SuccessfulDeals { get; set; }
}

public sealed class Address : SoftDeletableEntity<long>
{
    public long UserId { get; set; }
    public string? Label { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? Street { get; set; }
    public string? BuildingNo { get; set; }
    public string? PostalCode { get; set; }
    public string? AdditionalNo { get; set; }
    public string FullText { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class RefreshToken : CreatedEntity<long>
{
    private RefreshToken() { }

    public RefreshToken(long userId, string tokenHash, Guid familyId, DateTime expiresAt, long? deviceId, string? ip, string? userAgent)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
        DeviceId = deviceId;
        CreatedIp = ip;
        UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent;
    }

    public long UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public long? DeviceId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public RefreshRevokeReason? RevokedReason { get; private set; }
    public long? ReplacedByTokenId { get; private set; }
    public string? CreatedIp { get; private set; }
    public string? UserAgent { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTime now, RefreshRevokeReason reason, long? replacedBy = null)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        RevokedReason = reason;
        ReplacedByTokenId = replacedBy;
    }

    public void SetReplacedBy(long tokenId) => ReplacedByTokenId = tokenId;
}

public sealed class Device : Entity<long>
{
    public long UserId { get; set; }
    public string DeviceToken { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string? AppVersion { get; set; }
    public string? OsVersion { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class NafathVerification : CreatedEntity<long>
{
    public long UserId { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string RandomNumber { get; set; } = string.Empty;
    public NafathStatus Status { get; set; }
    public string? NationalIdHash { get; set; }
    public string? VerifiedName { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class Bank : Entity<long>
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public long? LogoFileId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class BankAccount : Entity<long>
{
    public long UserId { get; set; }
    public string HolderName { get; set; } = string.Empty;
    public string IbanEncrypted { get; set; } = string.Empty;
    public string IbanLast4 { get; set; } = string.Empty;
    public string IbanHash { get; set; } = string.Empty;
    public long BankId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class TermsVersion : Entity<long>
{
    public TermsType Type { get; set; }
    public string Version { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    /// <summary>JSON array of strings.</summary>
    public string ItemsAr { get; set; } = "[]";
    public string ItemsEn { get; set; } = "[]";
    public DateTime PublishedAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class TermsAcceptance : Entity<long>
{
    public long UserId { get; set; }
    public long TermsVersionId { get; set; }
    public long? DealId { get; set; }
    public long? CompanyDealId { get; set; }
    public DateTime AcceptedAt { get; set; }
    public string? Ip { get; set; }
    public long? DeviceId { get; set; }
}

public sealed class AuthAuditLog : CreatedEntity<long>
{
    public long? UserId { get; set; }
    public string? PhoneNormalized { get; set; }
    public AuthEvent Event { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public long? DeviceId { get; set; }
    public string? Details { get; set; }
}
