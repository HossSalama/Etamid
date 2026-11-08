using Etamid.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Etamid.Infrastructure.Persistence.Configurations;

public sealed class AdminRefreshTokenConfiguration : IEntityTypeConfiguration<AdminRefreshToken>
{
    public void Configure(EntityTypeBuilder<AdminRefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(255);
        b.Property(x => x.CreatedIp).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(300);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.AdminUserId);
        b.HasIndex(x => x.FamilyId);
        b.ForeignKey<AdminRefreshToken, AdminUser>(x => x.AdminUserId);
    }
}

public sealed class AdminTwoFactorChallengeConfiguration : IEntityTypeConfiguration<AdminTwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<AdminTwoFactorChallenge> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.AdminUserId);
        b.ForeignKey<AdminTwoFactorChallenge, AdminUser>(x => x.AdminUserId);
    }
}

// Seed data (second configuration classes for the same entities: EF merges them).
// Ids are fixed: permissions 1..18 in the order of AdminPermissionCodes.All, role 1 = super-admin.
public sealed class AdminPermissionSeed : IEntityTypeConfiguration<AdminPermission>
{
    public void Configure(EntityTypeBuilder<AdminPermission> b)
        => b.HasData(AdminPermissionCodes.All
            .Select((code, index) => (object)new { Id = (long)index + 1, Code = code, Description = code })
            .ToArray());
}

public sealed class AdminRoleSeed : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> b)
        => b.HasData(new { Id = 1L, Name = AdminRoleNames.SuperAdmin, Description = "Holds every admin permission. Protected: cannot be edited or deleted." });
}

public sealed class AdminRolePermissionSeed : IEntityTypeConfiguration<AdminRolePermission>
{
    public void Configure(EntityTypeBuilder<AdminRolePermission> b)
        => b.HasData(AdminPermissionCodes.All
            .Select((_, index) => (object)new { RoleId = 1L, PermissionId = (long)index + 1 })
            .ToArray());
}
