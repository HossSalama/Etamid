using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Infrastructure.Persistence;

/// <summary>
/// Local-only first super-admin. Called from Program.cs in Development only. Does nothing unless
/// AdminSeed:Email and AdminSeed:Password are provided (user-secrets or environment) AND no admin exists yet.
/// </summary>
public static class AdminBootstrapSeeder
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        var email = configuration["AdminSeed:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return;

        if (!AdminPasswordPolicy.IsValid(password))
            throw new InvalidOperationException("AdminSeed:Password must be 12-64 characters, no spaces, with a letter, a digit and a symbol.");

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await db.AdminUsers.AnyAsync(ct)) return;

        var roleId = await db.AdminRoles.Where(r => r.Name == AdminRoleNames.SuperAdmin).Select(r => r.Id).FirstAsync(ct);
        var seedName = configuration["AdminSeed:Name"]?.Trim();
        var admin = new AdminUser
        {
            Name = string.IsNullOrEmpty(seedName) ? "Super Admin" : seedName,
            Email = email,
            PasswordHash = hasher.Hash(password),
            Status = AdminStatus.Active
        };
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync(ct);

        db.AdminUserRoles.Add(new AdminUserRole { AdminUserId = admin.Id, RoleId = roleId });
        await db.SaveChangesAsync(ct);
    }
}
