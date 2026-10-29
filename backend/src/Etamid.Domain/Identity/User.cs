using Etamid.Domain.Common;
using Etamid.Domain.Identity.ValueObjects;

namespace Etamid.Domain.Identity;

public sealed class User : AuditableEntity<long>
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User() { }

    public string Code { get; private set; } = string.Empty;
    public UserType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string PhoneNormalized { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public AccountStatus Status { get; private set; }
    public DateTime? PhoneVerifiedAt { get; private set; }
    public DateTime? EmailVerifiedAt { get; private set; }
    public Language Language { get; private set; }
    public long? AvatarFileId { get; private set; }
    public bool NafathVerified { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    /// <summary>Auth time of the last accepted Firebase token; protects against token replay.</summary>
    public DateTime? LastPhoneAuthAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public CompanyProfile? CompanyProfile { get; private set; }

    public static User Register(
        UserType type, string code, string name, PhoneNumber phone, string email, string passwordHash, Language language)
        => new()
        {
            Type = type,
            Code = code,
            Name = name.Trim(),
            Phone = phone.Local,
            PhoneNormalized = phone.E164,
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Status = AccountStatus.PendingVerification,
            Language = language
        };

    /// <summary>An unverified account that registers again simply refreshes its data.</summary>
    public void RefreshUnverifiedRegistration(string name, string email, string passwordHash, Language language)
    {
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Language = language;
    }

    public void AttachCompanyProfile(CompanyProfile profile) => CompanyProfile = profile;

    public bool IsLocked(DateTime now) => LockedUntil.HasValue && LockedUntil.Value > now;

    public int RemainingLockSeconds(DateTime now)
        => IsLocked(now) ? (int)Math.Ceiling((LockedUntil!.Value - now).TotalSeconds) : 0;

    /// <returns>true when this failure locked the account.</returns>
    public bool RecordFailedLogin(DateTime now)
    {
        FailedLoginCount++;
        if (FailedLoginCount < MaxFailedLogins) return false;

        LockedUntil = now.Add(LockoutDuration);
        FailedLoginCount = 0;
        return true;
    }

    public void RecordSuccessfulLogin(DateTime now)
    {
        FailedLoginCount = 0;
        LockedUntil = null;
        LastLoginAt = now;
    }

    public void VerifyPhone(DateTime now, DateTime phoneAuthTime)
    {
        PhoneVerifiedAt = now;
        LastPhoneAuthAt = phoneAuthTime;
        if (Status == AccountStatus.PendingVerification)
            Status = AccountStatus.Active;
    }

    /// <summary>Registration is confirmed by a code sent to the email address.</summary>
    public void VerifyEmail(DateTime now)
    {
        EmailVerifiedAt = now;
        if (Status == AccountStatus.PendingVerification)
            Status = AccountStatus.Active;
    }

    public void AcceptPhoneAuth(DateTime phoneAuthTime) => LastPhoneAuthAt = phoneAuthTime;

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public void UpdateProfile(string? name, string? email)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLowerInvariant();
            // Changing the address must never inherit the old address's verification:
            // the new address is unverified until its own OTP is confirmed.
            if (!string.Equals(normalized, Email, StringComparison.Ordinal))
            {
                Email = normalized;
                EmailVerifiedAt = null;
            }
        }
    }

    public void ChangeLanguage(Language language) => Language = language;

    public void SetAvatar(long? fileId) => AvatarFileId = fileId;

    public void MarkNafathVerified() => NafathVerified = true;

    public void Suspend() => Status = AccountStatus.Suspended;

    public void MarkDeleted(DateTime now)
    {
        Status = AccountStatus.Deleted;
        DeletedAt = now;
    }
}
