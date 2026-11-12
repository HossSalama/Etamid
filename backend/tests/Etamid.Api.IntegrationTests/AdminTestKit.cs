using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>An admin signed in through /admin/api/auth/login. Client carries the AdminJwt access token.</summary>
public sealed record AdminClient(HttpClient Client, long AdminId);

/// <summary>Test helper: seeds an admin whose role holds exactly the given permission codes, then signs in for real.</summary>
public static class AdminTestKit
{
    public const string Password = "Adm1n#Passw0rd!";

    public static async Task<AdminClient> CreateAsync(EtamidFactory factory, params string[] permissions)
    {
        var email = $"kit.{Guid.NewGuid():N}@example.com";
        long adminId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var admin = new AdminUser
            {
                Name = "Kit Admin", Email = email, PasswordHash = hasher.Hash(Password), Status = AdminStatus.Active
            };
            db.AdminUsers.Add(admin);
            await db.SaveChangesAsync();
            adminId = admin.Id;

            if (permissions.Length > 0)
            {
                var role = new AdminRole { Name = "kit-" + Guid.NewGuid().ToString("N"), Description = "test role" };
                db.AdminRoles.Add(role);
                await db.SaveChangesAsync();

                var permissionIds = await db.AdminPermissions.AsNoTracking()
                    .Where(p => permissions.Contains(p.Code)).Select(p => p.Id).ToListAsync();
                if (permissionIds.Count != permissions.Length)
                    throw new InvalidOperationException("Unknown permission code in test setup.");

                foreach (var permissionId in permissionIds)
                    db.AdminRolePermissions.Add(new AdminRolePermission { RoleId = role.Id, PermissionId = permissionId });
                db.AdminUserRoles.Add(new AdminUserRole { AdminUserId = admin.Id, RoleId = role.Id });
                await db.SaveChangesAsync();
            }
        }

        using var anon = factory.CreateClient();
        var login = await anon.PostAsJsonAsync("/admin/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement
            .GetProperty("data").GetProperty("auth").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new AdminClient(client, adminId);
    }
}
