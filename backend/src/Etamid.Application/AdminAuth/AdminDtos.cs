using Etamid.Domain.Admin;

namespace Etamid.Application.AdminAuth;

public sealed record AdminDto(
    long Id, string Name, string Email, AdminStatus Status, bool TwoFactorEnabled, DateTime? LastLoginAt,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public sealed record AdminListItemDto(
    long Id, string Name, string Email, AdminStatus Status, bool TwoFactorEnabled, DateTime? LastLoginAt,
    DateTime CreatedAt, IReadOnlyList<string> Roles);

public sealed record AdminAuthResultDto(string AccessToken, string RefreshToken, int ExpiresInSeconds, AdminDto Admin);

public sealed record AdminChallengeDto(string ChallengeToken, int ExpiresInSeconds);

/// <summary>Either a 2FA challenge (no tokens) or the full token set.</summary>
public sealed record AdminLoginResultDto(bool TwoFactorRequired, AdminChallengeDto? Challenge, AdminAuthResultDto? Auth);

public sealed record AdminTwoFactorSetupDto(string Secret, string OtpAuthUri);

public sealed record AdminRoleDto(long Id, string Name, string? Description, IReadOnlyList<string> Permissions);

public sealed record AdminPermissionDto(long Id, string Code, string? Description);

public sealed record AuditLogDto(
    long Id, AuditActorType ActorType, long? ActorId, string Action, string EntityType, long EntityId,
    string? OldValues, string? NewValues, string? Ip, DateTime CreatedAt);
