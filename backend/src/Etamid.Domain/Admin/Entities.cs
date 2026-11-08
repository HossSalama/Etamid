using Etamid.Domain.Common;
using Etamid.SharedKernel;

namespace Etamid.Domain.Admin;

public enum AdminStatus { Active, Disabled }
public enum AuditActorType { Admin, User, System }
public enum OutboxStatus { Pending, Processed, Failed }

public sealed class AdminUser : AuditableEntity<long>
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }
    public AdminStatus Status { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

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
}

public sealed class AdminRole : Entity<long>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class AdminPermission : Entity<long>
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class AdminRolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
}

public sealed class AdminUserRole
{
    public long AdminUserId { get; set; }
    public long RoleId { get; set; }
}

public sealed class AuditLog : CreatedEntity<long>
{
    public AuditActorType ActorType { get; set; }
    public long? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? Ip { get; set; }
}

public sealed class OutboxMessage : CreatedEntity<long>
{
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
