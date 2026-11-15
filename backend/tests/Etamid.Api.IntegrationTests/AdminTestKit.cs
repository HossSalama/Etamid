using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

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
        // 2FA is mandatory: a 2FA-off admin gets no tokens from login, so signing in
        // completes the setup-challenge -> enable-challenge flow first.
        var root = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        string token;
        if (root.TryGetProperty("auth", out var auth) && auth.ValueKind != JsonValueKind.Null)
        {
            token = auth.GetProperty("accessToken").GetString()!;
        }
        else if (root.GetProperty("twoFactorSetupRequired").GetBoolean())
        {
            var challenge = root.GetProperty("challenge").GetProperty("challengeToken").GetString()!;
            var setup = await anon.PostAsJsonAsync("/admin/api/auth/2fa/setup-challenge", new { challengeToken = challenge });
            setup.EnsureSuccessStatusCode();
            var secret = JsonDocument.Parse(await setup.Content.ReadAsStringAsync()).RootElement
                .GetProperty("data").GetProperty("secret").GetString()!;
            var code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();
            var enable = await anon.PostAsJsonAsync("/admin/api/auth/2fa/enable-challenge", new { challengeToken = challenge, code });
            enable.EnsureSuccessStatusCode();
            token = JsonDocument.Parse(await enable.Content.ReadAsStringAsync()).RootElement
                .GetProperty("data").GetProperty("accessToken").GetString()!;
        }
        else
        {
            throw new InvalidOperationException("AdminTestKit: login returned neither tokens nor a 2FA setup challenge.");
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new AdminClient(client, adminId);
    }
}
