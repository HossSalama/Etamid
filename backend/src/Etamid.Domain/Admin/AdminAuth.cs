using Etamid.Domain.Common;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;

namespace Etamid.Domain.Admin;

public static class AdminRoleNames
{
    public const string SuperAdmin = "super-admin";
}

/// <summary>The 18 permission codes. Seeded in the database; checked per request from the database.</summary>
public static class AdminPermissionCodes
{
    public const string UsersView = "admin.users.view";
    public const string UsersManage = "admin.users.manage";
    public const string UsersNafathReview = "admin.users.nafath-review";
    public const string DealsView = "admin.deals.view";
    public const string DealsManage = "admin.deals.manage";
    public const string DisputesView = "admin.disputes.view";
    public const string DisputesDecide = "admin.disputes.decide";
    public const string WithdrawalsView = "admin.withdrawals.view";
    public const string WithdrawalsApprove = "admin.withdrawals.approve";
    public const string WithdrawalsComplete = "admin.withdrawals.complete";
    public const string PenaltiesView = "admin.penalties.view";
    public const string PenaltiesWaive = "admin.penalties.waive";
    public const string ContentManage = "admin.content.manage";
    public const string SettingsManage = "admin.settings.manage";
    public const string NotificationsSend = "admin.notifications.send";
    public const string SupportManage = "admin.support.manage";
    public const string RolesManage = "admin.roles.manage";
    public const string AuditView = "admin.audit.view";

    public static readonly IReadOnlyList<string> All =
    [
        UsersView, UsersManage, UsersNafathReview, DealsView, DealsManage, DisputesView, DisputesDecide,
        WithdrawalsView, WithdrawalsApprove, WithdrawalsComplete, PenaltiesView, PenaltiesWaive,
        ContentManage, SettingsManage, NotificationsSend, SupportManage, RolesManage, AuditView
    ];
}

/// <summary>Admin passwords are stricter than app-user passwords: 12-64 chars, no spaces, letter + digit + symbol.</summary>
public static class AdminPasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 64;

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        if (password.Length is < MinLength or > MaxLength) return false;
        if (password.Any(char.IsWhiteSpace)) return false;
        return password.Any(char.IsLetter) && password.Any(char.IsDigit) && password.Any(c => !char.IsLetterOrDigit(c));
    }
}

/// <summary>Admin login session token. Mirrors RefreshToken; stored hashed only, in its own table.</summary>
public sealed class AdminRefreshToken : CreatedEntity<long>
{
    private AdminRefreshToken() { }

    public AdminRefreshToken(long adminUserId, string tokenHash, Guid familyId, DateTime expiresAt, string? ip, string? userAgent)
    {
        AdminUserId = adminUserId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        ExpiresAt = expiresAt;
        CreatedIp = ip;
        UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent;
    }

    public long AdminUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
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

/// <summary>Single-use, 5-minute proof that the password step passed; exchanged for tokens with a valid TOTP code.</summary>
public sealed class AdminTwoFactorChallenge : CreatedEntity<long>
{
    public long AdminUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
