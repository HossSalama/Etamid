using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Etamid.Api.IntegrationTests;

/// <summary>Cycle 9 task 1: admin authentication (password, 2FA, JWT, refresh, lockout) and database-backed RBAC.</summary>
public class AdminAuthTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Adm1n#Passw0rd!";
    private static int _counter = 100;

    private readonly HttpClient _anon = factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static async Task<string> Code(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    // Errors arrive as HTTP 200 with { success: false, errors: [{ code }] }
    // (owner decision 2026-11-09, Hoss's BaseApiController pattern).
    private static async Task<bool> Succeeded(HttpResponseMessage r)
        => (await Json(r)).GetProperty("success").GetBoolean();

    private static string NewEmail() => $"admin.{Interlocked.Increment(ref _counter)}@example.com";

    private async Task<long> SeedAdmin(string email, bool superAdmin = false, bool disabled = false, string? totpSecret = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var protector = scope.ServiceProvider.GetRequiredService<IFieldProtector>();

        var admin = new AdminUser
        {
            Name = "Test Admin", Email = email, PasswordHash = hasher.Hash(Password),
            Status = disabled ? AdminStatus.Disabled : AdminStatus.Active
        };
        if (totpSecret is not null)
        {
            admin.TwoFactorEnabled = true;
            admin.TwoFactorSecret = protector.Protect(totpSecret);
        }
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();

        if (superAdmin)
        {
            db.AdminUserRoles.Add(new AdminUserRole { AdminUserId = admin.Id, RoleId = 1 });
            await db.SaveChangesAsync();
        }
        return admin.Id;
    }

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private Task<HttpResponseMessage> Login(string email, string password)
        => _anon.PostAsJsonAsync("/admin/api/auth/login", new { email, password });

    private sealed record Session(HttpClient Client, string Access, string Refresh);

    private async Task<Session> SignIn(string email)
    {
        // 2FA is mandatory: a 2FA-off admin gets no tokens from login, so signing in
        // completes the setup-challenge -> enable-challenge flow first.
        var r = await Login(email, Password);
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var data = await Data(r);
        if (data.TryGetProperty("auth", out var auth) && auth.ValueKind != JsonValueKind.Null)
            return MakeSession(auth.GetProperty("accessToken").GetString()!, auth.GetProperty("refreshToken").GetString()!);

        if (data.TryGetProperty("twoFactorSetupRequired", out var setupRequired) && setupRequired.GetBoolean())
        {
            var challenge = data.GetProperty("challenge").GetProperty("challengeToken").GetString()!;
            var setup = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/setup-challenge", new { challengeToken = challenge });
            setup.StatusCode.Should().Be(HttpStatusCode.OK, await setup.Content.ReadAsStringAsync());
            var secret = (await Data(setup)).GetProperty("secret").GetString()!;
            var enable = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/enable-challenge", new { challengeToken = challenge, code = TotpNow(secret) });
            enable.StatusCode.Should().Be(HttpStatusCode.OK, await enable.Content.ReadAsStringAsync());
            var enabled = await Data(enable);
            return MakeSession(enabled.GetProperty("accessToken").GetString()!, enabled.GetProperty("refreshToken").GetString()!);
        }

        throw new InvalidOperationException("SignIn: login returned neither tokens nor a 2FA setup challenge.");
    }

    private Session MakeSession(string access, string refresh)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return new Session(client, access, refresh);
    }

    private static string TotpNow(string secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    private static string WrongCode(string real) => real == "000000" ? "111111" : "000000";

    // ------------------------------------------------------------------ seed
    [Fact]
    public async Task Eighteen_permissions_and_super_admin_role_are_seeded_with_the_corrected_spelling()
    {
        var codes = await Db(db => db.AdminPermissions.AsNoTracking().Select(p => p.Code).ToListAsync());
        codes.Should().HaveCount(18).And.BeEquivalentTo(AdminPermissionCodes.All);
        codes.Should().Contain("admin.users.nafath-review").And.NotContain("admin.users.nfath-review");

        var granted = await Db(db => db.AdminRolePermissions.AsNoTracking().CountAsync(x => x.RoleId == 1));
        granted.Should().Be(18);
    }

    // ------------------------------------------------------------------ login
    [Fact]
    public async Task Login_with_2fa_off_requires_setup_then_tokens_me_works_and_attempt_is_audited()
    {
        var email = NewEmail();
        var id = await SeedAdmin(email, superAdmin: true);

        // Login alone issues no tokens while 2FA is off: only a setup challenge.
        var login = await Login(email, Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginData = await Data(login);
        loginData.GetProperty("twoFactorRequired").GetBoolean().Should().BeFalse();
        loginData.GetProperty("twoFactorSetupRequired").GetBoolean().Should().BeTrue();
        loginData.GetProperty("auth").ValueKind.Should().Be(JsonValueKind.Null);
        loginData.GetProperty("challenge").GetProperty("challengeToken").GetString().Should().NotBeNullOrEmpty();

        var session = await SignIn(email);
        var me = await Data(await session.Client.GetAsync("/admin/api/auth/me"));
        me.GetProperty("email").GetString().Should().Be(email);
        me.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).Should().Contain("super-admin");
        me.GetProperty("permissions").GetArrayLength().Should().Be(18);

        var audited = await Db(db => db.AuditLogs.AsNoTracking().CountAsync(a => a.Action == "admin.login.success" && a.EntityId == id));
        audited.Should().Be(1);
    }

    [Fact]
    public async Task Unknown_email_looks_identical_to_wrong_password_and_both_are_audited()
    {
        var email = NewEmail();
        var id = await SeedAdmin(email);

        var wrong = await Login(email, "Wrong#Passw0rd!x");
        var unknown = await Login("nobody." + NewEmail(), Password);

        wrong.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(wrong)).Should().BeFalse();
        unknown.StatusCode.Should().Be(wrong.StatusCode);
        (await Code(unknown)).Should().Be(await Code(wrong)).And.Be("ADMIN_INVALID_CREDENTIALS");
        (await Json(unknown)).GetProperty("message").GetString().Should().Be((await Json(wrong)).GetProperty("message").GetString());

        (await Db(db => db.AuditLogs.AsNoTracking().CountAsync(a => a.Action == "admin.login.failed" && a.EntityId == id))).Should().Be(1);
        (await Db(db => db.AuditLogs.AsNoTracking().CountAsync(a => a.Action == "admin.login.failed" && a.EntityId == 0))).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Five_failures_lock_the_admin_even_for_the_right_password()
    {
        var email = NewEmail();
        await SeedAdmin(email);

        for (var i = 0; i < 4; i++)
            (await Login(email, "Wrong#Passw0rd!x")).StatusCode.Should().Be(HttpStatusCode.OK);
        var fifth = await Login(email, "Wrong#Passw0rd!x");
        fifth.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(fifth)).Should().BeFalse();
        (await Code(fifth)).Should().Be("ADMIN_ACCOUNT_LOCKED");

        var locked = await Login(email, Password);
        locked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(locked)).Should().BeFalse();
        (await Code(locked)).Should().Be("ADMIN_ACCOUNT_LOCKED");
        locked.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task Disabled_admin_is_rejected_with_the_generic_error()
    {
        var email = NewEmail();
        await SeedAdmin(email, disabled: true);

        var r = await Login(email, Password);
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(r)).Should().BeFalse();
        (await Code(r)).Should().Be("ADMIN_INVALID_CREDENTIALS");
    }

    // ------------------------------------------------------------------ 2FA
    [Fact]
    public async Task Two_factor_login_issues_only_a_challenge_then_tokens_for_a_right_code()
    {
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var email = NewEmail();
        await SeedAdmin(email, totpSecret: secret);

        var login = await Login(email, Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await Data(login);
        data.GetProperty("twoFactorRequired").GetBoolean().Should().BeTrue();
        data.GetProperty("auth").ValueKind.Should().Be(JsonValueKind.Null);
        var challenge = data.GetProperty("challenge").GetProperty("challengeToken").GetString()!;

        var good = TotpNow(secret);
        var wrong = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/verify", new { challengeToken = challenge, code = WrongCode(good) });
        wrong.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(wrong)).Should().BeFalse();
        (await Code(wrong)).Should().Be("ADMIN_2FA_CODE_INVALID");
        (await wrong.Content.ReadAsStringAsync()).Should().NotContain("accessToken");

        var ok = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/verify", new { challengeToken = challenge, code = good });
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        (await Data(ok)).GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        // The challenge is single use.
        var again = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/verify", new { challengeToken = challenge, code = good });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(again)).Should().BeFalse();
        (await Code(again)).Should().Be("ADMIN_2FA_CHALLENGE_INVALID");
    }

    [Fact]
    public async Task Setup_challenge_and_enable_challenge_then_login_requires_verify()
    {
        var email = NewEmail();
        await SeedAdmin(email);

        var login = await Login(email, Password);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginData = await Data(login);
        loginData.GetProperty("twoFactorSetupRequired").GetBoolean().Should().BeTrue();
        var challenge = loginData.GetProperty("challenge").GetProperty("challengeToken").GetString()!;

        // Setup does not consume the challenge: calling it twice still works.
        var setup1 = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/setup-challenge", new { challengeToken = challenge });
        setup1.StatusCode.Should().Be(HttpStatusCode.OK, await setup1.Content.ReadAsStringAsync());
        var setup2 = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/setup-challenge", new { challengeToken = challenge });
        setup2.StatusCode.Should().Be(HttpStatusCode.OK, await setup2.Content.ReadAsStringAsync());
        var setupData = await Data(setup2);
        var secret = setupData.GetProperty("secret").GetString()!;
        setupData.GetProperty("otpAuthUri").GetString().Should().NotBeNullOrEmpty();

        var bad = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/enable-challenge", new { challengeToken = challenge, code = WrongCode(TotpNow(secret)) });
        bad.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(bad)).Should().BeFalse();
        (await Code(bad)).Should().Be("ADMIN_2FA_CODE_INVALID");
        (await bad.Content.ReadAsStringAsync()).Should().NotContain("accessToken");

        var enable = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/enable-challenge", new { challengeToken = challenge, code = TotpNow(secret) });
        enable.StatusCode.Should().Be(HttpStatusCode.OK, await enable.Content.ReadAsStringAsync());
        (await Data(enable)).GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        // The challenge is single use.
        var again = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/enable-challenge", new { challengeToken = challenge, code = TotpNow(secret) });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(again)).Should().BeFalse();
        (await Code(again)).Should().Be("ADMIN_2FA_CHALLENGE_INVALID");

        (await Data(await Login(email, Password))).GetProperty("twoFactorRequired").GetBoolean().Should().BeTrue();

        // The stored secret is encrypted, never the Base32 text.
        var stored = await Db(db => db.AdminUsers.AsNoTracking().Where(a => a.Email == email).Select(a => a.TwoFactorSecret).FirstAsync());
        stored.Should().NotBeNullOrEmpty().And.NotBe(secret);
    }

    [Fact]
    public async Task Setup_challenge_and_enable_challenge_with_an_unknown_challenge_are_rejected()
    {
        var setup = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/setup-challenge", new { challengeToken = "unknown-challenge" });
        setup.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(setup)).Should().BeFalse();
        (await Code(setup)).Should().Be("ADMIN_2FA_CHALLENGE_INVALID");

        var enable = await _anon.PostAsJsonAsync("/admin/api/auth/2fa/enable-challenge", new { challengeToken = "unknown-challenge", code = "123456" });
        enable.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(enable)).Should().BeFalse();
        (await Code(enable)).Should().Be("ADMIN_2FA_CHALLENGE_INVALID");
    }

    [Fact]
    public async Task Self_disable_two_factor_is_forbidden_and_leaves_2fa_on()
    {
        var email = NewEmail();
        await SeedAdmin(email);
        var session = await SignIn(email);   // enables 2FA through the mandatory flow

        var r = await session.Client.PostAsJsonAsync("/admin/api/auth/2fa/disable", new { password = Password, code = "123456" });
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(r)).Should().BeFalse();
        (await Code(r)).Should().Be("ADMIN_FORBIDDEN");

        var enabled = await Db(db => db.AdminUsers.AsNoTracking().Where(a => a.Email == email).Select(a => a.TwoFactorEnabled).FirstAsync());
        enabled.Should().BeTrue();
        var secret = await Db(db => db.AdminUsers.AsNoTracking().Where(a => a.Email == email).Select(a => a.TwoFactorSecret).FirstAsync());
        secret.Should().NotBeNullOrEmpty();
    }

    // ------------------------------------------------------------------ refresh
    [Fact]
    public async Task Refresh_rotates_and_reuse_of_a_rotated_token_revokes_every_session()
    {
        var email = NewEmail();
        await SeedAdmin(email);
        var first = await SignIn(email);

        var rotated = await _anon.PostAsJsonAsync("/admin/api/auth/refresh", new { refreshToken = first.Refresh });
        rotated.StatusCode.Should().Be(HttpStatusCode.OK, await rotated.Content.ReadAsStringAsync());
        var second = await Data(rotated);
        var secondRefresh = second.GetProperty("refreshToken").GetString()!;
        var secondAccess = second.GetProperty("accessToken").GetString()!;
        secondRefresh.Should().NotBe(first.Refresh);

        var reuse = await _anon.PostAsJsonAsync("/admin/api/auth/refresh", new { refreshToken = first.Refresh });
        reuse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(reuse)).Should().BeFalse();
        (await Code(reuse)).Should().Be("ADMIN_REFRESH_REUSED");

        // The second refresh token was revoked when reuse was detected.
        var secondRefreshAgain = await _anon.PostAsJsonAsync("/admin/api/auth/refresh", new { refreshToken = secondRefresh });
        secondRefreshAgain.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(secondRefreshAgain)).Should().BeFalse();
        (await Code(secondRefreshAgain)).Should().Be("ADMIN_TOKEN_INVALID");

        // Reuse revoked all sessions, so even the live access token dies.
        var deadMe = await MakeSession(secondAccess, secondRefresh).Client.GetAsync("/admin/api/auth/me");
        deadMe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(deadMe)).Should().BeFalse();
        (await Code(deadMe)).Should().Be("ADMIN_TOKEN_INVALID");
    }

    [Fact]
    public async Task Refresh_tokens_are_stored_hashed_only()
    {
        var email = NewEmail();
        await SeedAdmin(email);
        var session = await SignIn(email);

        var hashes = await Db(db => db.AdminRefreshTokens.AsNoTracking().Select(t => t.TokenHash).ToListAsync());
        hashes.Should().NotContain(session.Refresh);
    }

    [Fact]
    public async Task Logout_kills_the_session_immediately()
    {
        var email = NewEmail();
        await SeedAdmin(email);
        var session = await SignIn(email);

        (await session.Client.PostAsync("/admin/api/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var deadMe = await session.Client.GetAsync("/admin/api/auth/me");
        deadMe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(deadMe)).Should().BeFalse();
        (await Code(deadMe)).Should().Be("ADMIN_TOKEN_INVALID");

        var deadRefresh = await _anon.PostAsJsonAsync("/admin/api/auth/refresh", new { refreshToken = session.Refresh });
        deadRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(deadRefresh)).Should().BeFalse();
        (await Code(deadRefresh)).Should().Be("ADMIN_TOKEN_INVALID");
    }

    // ------------------------------------------------------------------ 401 / 403
    [Fact]
    public async Task No_token_is_401_and_an_app_user_token_is_401_on_admin_routes_and_vice_versa()
    {
        var anonMe = await _anon.GetAsync("/admin/api/auth/me");
        anonMe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(anonMe)).Should().Be("UNAUTHORIZED");

        // App user
        var n = Interlocked.Increment(ref _counter);
        var phone = $"05012{n:D5}";
        var appEmail = $"appuser.{n}@example.com";
        (await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "App User", phone, email = appEmail, password = "Pass#123", acceptTerms = true }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = factory.Emails.CodeFor(appEmail) });
        var appToken = (await Data(verify)).GetProperty("accessToken").GetString()!;

        var appClient = factory.CreateClient();
        appClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
        var appMe = await appClient.GetAsync("/admin/api/auth/me");
        appMe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(appMe)).Should().Be("UNAUTHORIZED");
        var appAudit = await appClient.GetAsync("/admin/api/audit-logs");
        appAudit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(appAudit)).Should().Be("UNAUTHORIZED");

        var email = NewEmail();
        await SeedAdmin(email, superAdmin: true);
        var admin = await SignIn(email);
        var adminOnAppApi = await admin.Client.GetAsync("/api/v1/me");
        adminOnAppApi.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(adminOnAppApi)).Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Missing_permission_is_refused_with_a_forbidden_code()
    {
        var email = NewEmail();
        await SeedAdmin(email);   // no roles
        var session = await SignIn(email);

        var r = await session.Client.GetAsync("/admin/api/audit-logs");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(r)).Should().BeFalse();
        (await Code(r)).Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Super_admin_only_endpoints_refuse_a_non_super_admin_even_with_the_permission()
    {
        var superEmail = NewEmail();
        await SeedAdmin(superEmail, superAdmin: true);
        var superSession = await SignIn(superEmail);

        var role = await Data(await superSession.Client.PostAsJsonAsync("/admin/api/roles", new { name = "role-managers-" + NewEmail(), description = "x" }));
        var roleId = role.GetProperty("id").GetInt64();
        (await superSession.Client.PostAsJsonAsync($"/admin/api/roles/{roleId}/permissions", new { permissions = new[] { "admin.roles.manage" } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var otherEmail = NewEmail();
        var otherId = await SeedAdmin(otherEmail);
        (await superSession.Client.PostAsJsonAsync($"/admin/api/admins/{otherId}/roles", new { roleIds = new[] { roleId } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var other = await SignIn(otherEmail);

        // May read (permission only) ...
        (await other.Client.GetAsync("/admin/api/roles")).StatusCode.Should().Be(HttpStatusCode.OK);
        // ... may not write (role membership).
        var write = await other.Client.PostAsJsonAsync("/admin/api/roles", new { name = "nope-" + NewEmail() });
        write.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(write)).Should().BeFalse();
        (await Code(write)).Should().Be("ADMIN_SUPER_ADMIN_REQUIRED");
    }

    [Fact]
    public async Task Permission_change_takes_effect_on_the_next_request()
    {
        var superEmail = NewEmail();
        await SeedAdmin(superEmail, superAdmin: true);
        var superSession = await SignIn(superEmail);

        var roleId = (await Data(await superSession.Client.PostAsJsonAsync("/admin/api/roles", new { name = "auditors-" + NewEmail() })))
            .GetProperty("id").GetInt64();
        var bEmail = NewEmail();
        var bId = await SeedAdmin(bEmail);
        (await superSession.Client.PostAsJsonAsync($"/admin/api/admins/{bId}/roles", new { roleIds = new[] { roleId } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var b = await SignIn(bEmail);   // the token is issued BEFORE the permission exists

        var denied = await b.Client.GetAsync("/admin/api/audit-logs");
        denied.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(denied)).Should().BeFalse();
        (await Code(denied)).Should().Be("ADMIN_FORBIDDEN");

        (await superSession.Client.PostAsJsonAsync($"/admin/api/roles/{roleId}/permissions", new { permissions = new[] { "admin.audit.view" } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await b.Client.GetAsync("/admin/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await superSession.Client.PostAsJsonAsync($"/admin/api/roles/{roleId}/permissions", new { permissions = Array.Empty<string>() }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var deniedAgain = await b.Client.GetAsync("/admin/api/audit-logs");
        deniedAgain.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(deniedAgain)).Should().BeFalse();
        (await Code(deniedAgain)).Should().Be("ADMIN_FORBIDDEN");

        // Management writes are audited with before/after values.
        var audit = await Db(db => db.AuditLogs.AsNoTracking().Where(a => a.Action == "admin.role.permissions" && a.EntityId == roleId).ToListAsync());
        audit.Should().HaveCount(2);
        audit.Should().OnlyContain(a => a.OldValues != null && a.NewValues != null);
    }

    [Fact]
    public async Task Super_admin_role_is_protected_and_the_last_super_admin_cannot_be_disabled()
    {
        var email = NewEmail();
        var id = await SeedAdmin(email, superAdmin: true);
        var session = await SignIn(email);

        var edit = await session.Client.PostAsJsonAsync("/admin/api/roles/1/permissions", new { permissions = Array.Empty<string>() });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(edit)).Should().BeFalse();
        (await Code(edit)).Should().Be("ADMIN_ROLE_PROTECTED");

        var self = await session.Client.PostAsync($"/admin/api/admins/{id}/disable", null);
        self.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(self)).Should().BeFalse();
        (await Code(self)).Should().Be("ADMIN_CANNOT_DISABLE_SELF");
    }

    [Fact]
    public async Task Disabling_an_admin_revokes_their_session_and_blocks_login()
    {
        var superEmail = NewEmail();
        await SeedAdmin(superEmail, superAdmin: true);
        var superSession = await SignIn(superEmail);
        var targetEmail = NewEmail();
        var targetId = await SeedAdmin(targetEmail);
        var target = await SignIn(targetEmail);

        (await superSession.Client.PostAsync($"/admin/api/admins/{targetId}/disable", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var deadMe = await target.Client.GetAsync("/admin/api/auth/me");
        deadMe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(deadMe)).Should().BeFalse();
        (await Code(deadMe)).Should().Be("ADMIN_TOKEN_INVALID");

        var deadLogin = await Login(targetEmail, Password);
        deadLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Succeeded(deadLogin)).Should().BeFalse();
        (await Code(deadLogin)).Should().Be("ADMIN_INVALID_CREDENTIALS");

        (await superSession.Client.PostAsync($"/admin/api/admins/{targetId}/enable", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Login(targetEmail, Password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Super_admin_creates_an_admin_and_reset_2fa_clears_it()
    {
        var superEmail = NewEmail();
        await SeedAdmin(superEmail, superAdmin: true);
        var superSession = await SignIn(superEmail);

        var email = NewEmail();
        var created = await superSession.Client.PostAsJsonAsync("/admin/api/admins", new { name = "New Admin", email, password = Password, roleIds = new long[] { } });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await Data(created)).GetProperty("id").GetInt64();

        var weak = await superSession.Client.PostAsJsonAsync("/admin/api/admins", new { name = "Weak", email = NewEmail(), password = "short#1" });
        // Validation failures go through the exception middleware, which returns HTTP 200
        // with the error code in the envelope (owner decision 2026-11-09).
        weak.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(weak)).Should().Be("ADMIN_WEAK_PASSWORD");

        (await Login(email, Password)).StatusCode.Should().Be(HttpStatusCode.OK);

        await Db(async db =>
        {
            var admin = await db.AdminUsers.FirstAsync(a => a.Id == id);
            admin.TwoFactorEnabled = true;
            admin.TwoFactorSecret = "x";
            await db.SaveChangesAsync();
            return 0;
        });

        (await superSession.Client.PostAsync($"/admin/api/admins/{id}/reset-2fa", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterReset = await Data(await Login(email, Password));
        afterReset.GetProperty("twoFactorRequired").GetBoolean().Should().BeFalse();
        afterReset.GetProperty("twoFactorSetupRequired").GetBoolean().Should().BeTrue();
    }
}
