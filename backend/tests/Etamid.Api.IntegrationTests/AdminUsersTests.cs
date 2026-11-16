using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Cycle 9 task 4: admin users list / detail / soft delete. Errors are asserted from the response BODY (error code), never
/// from the HTTP status, because main answers API errors with HTTP 200 and the code in the body.
/// </summary>
public class AdminUsersTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 14000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient? Client, string Phone, string Name, string Email, long Id);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static async Task<bool> Succeeded(HttpResponseMessage r)
        => (await Json(r)).TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;

    /// <summary>The first error code in the body, or null when the call succeeded.</summary>
    private static async Task<string?> ErrorCode(HttpResponseMessage r)
    {
        var root = await Json(r);
        return root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
            ? errors[0].GetProperty("code").GetString()
            : null;
    }

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private Task<long> IdByPhone(string phone) => Db(db => db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync());

    private async Task<HttpClient> SignIn(string phone, string email)
    {
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = factory.Emails.CodeFor(email) });
        var token = (await Data(verify)).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>An individual. verified = false leaves it in PendingVerification (no token).</summary>
    private async Task<Party> NewIndividual(bool verified = true, string? namePart = null)
    {
        var n = Interlocked.Increment(ref _counter);
        var phone = $"05082{n:D5}";
        var email = $"usr.{n}@example.com";
        var name = $"Admin Users Test {namePart ?? n.ToString()}";

        var reg = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = name, phone, email, password = Password, acceptTerms = true });
        (await Succeeded(reg)).Should().BeTrue(await reg.Content.ReadAsStringAsync());
        var client = verified ? await SignIn(phone, email) : null;
        return new Party(client, phone, name, email, await IdByPhone(phone));
    }

    private async Task<Party> NewCompany()
    {
        var n = Interlocked.Increment(ref _counter);
        var phone = $"05082{n:D5}";
        var email = $"company-usr.{n}@example.com";
        var name = $"Admin Users Co {n}";

        var reg = await _anon.PostAsJsonAsync("/api/v1/auth/register/company",
            new { companyName = name, ownerPhone = phone, commercialRegNo = "CRAU" + n, taxNo = "TAX" + n, email, password = Password, acceptTerms = true });
        (await Succeeded(reg)).Should().BeTrue(await reg.Content.ReadAsStringAsync());
        var client = await SignIn(phone, email);
        return new Party(client, phone, name, email, await IdByPhone(phone));
    }

    private static async Task<long> CreateDeal(Party buyer, Party seller)
    {
        var r = await seller.Client!.PostAsJsonAsync("/api/v1/deals", new
        {
            title = "Admin users test deal", buyerPhoneOrCode = buyer.Phone, description = "Test",
            amount = 500m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        (await Succeeded(r)).Should().BeTrue(await r.Content.ReadAsStringAsync());
        return (await Data(r)).GetProperty("id").GetInt64();
    }

    /// <summary>A company deal in Draft (not finished). One installment, summing to the total.</summary>
    private static async Task<long> CreateCompanyDealDraft(Party creator, Party counterparty)
    {
        var due = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd");
        var r = await creator.Client!.PostAsJsonAsync("/api/v1/company-deals", new
        {
            counterpartyPhoneOrCode = counterparty.Phone,
            title = "Admin users test company deal", details = "Test",
            totalAmount = 1000m, startDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            approvalSendDaysBefore = 7, approvalResponseDays = 3,
            installments = new[] { new { label = "Only", amount = 1000m, dueDate = due } }
        });
        (await Succeeded(r)).Should().BeTrue(await r.Content.ReadAsStringAsync());
        return (await Data(r)).GetProperty("id").GetInt64();
    }

    private static IEnumerable<long> Ids(JsonElement items) => items.EnumerateArray().Select(i => i.GetProperty("id").GetInt64());

    private async Task<JsonElement> List(AdminClient admin, string query = "")
    {
        var r = await admin.Client.GetAsync("/admin/api/users?pageSize=100" + query);
        (await Succeeded(r)).Should().BeTrue(await r.Content.ReadAsStringAsync());
        return await Data(r);
    }

    // ------------------------------------------------------------------ list
    [Fact]
    public async Task List_filters_by_type_and_returns_both_types_without_a_type()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.view");
        var person1 = await NewIndividual();
        var person2 = await NewIndividual();
        var company = await NewCompany();

        var companies = await List(admin, "&type=company");
        Ids(companies).Should().Contain(company.Id).And.NotContain(new[] { person1.Id, person2.Id });
        companies.EnumerateArray().Should().OnlyContain(i => i.GetProperty("type").GetString() == "company");

        var individuals = await List(admin, "&type=individual");
        Ids(individuals).Should().Contain(new[] { person1.Id, person2.Id }).And.NotContain(company.Id);
        individuals.EnumerateArray().Should().OnlyContain(i => i.GetProperty("type").GetString() == "individual");

        Ids(await List(admin)).Should().Contain(new[] { person1.Id, person2.Id, company.Id });
        Ids(await List(admin, "&type=COMPANY")).Should().Contain(company.Id);   // case-insensitive

        var first = (await List(admin, "&type=company")).EnumerateArray().First(i => i.GetProperty("id").GetInt64() == company.Id);
        first.GetProperty("name").GetString().Should().Be(company.Name);
        first.GetProperty("email").GetString().Should().Be(company.Email);
        first.GetProperty("phone").GetString().Should().Be(company.Phone);
        first.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        first.GetProperty("status").GetString().Should().Be("active");
        first.TryGetProperty("nafathVerified", out _).Should().BeTrue();
        first.TryGetProperty("createdAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task List_status_and_search_filters_and_invalid_values()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.view");
        var token = Guid.NewGuid().ToString("N")[..10];
        var active = await NewIndividual(verified: true, namePart: "Active " + token);
        var pending = await NewIndividual(verified: false, namePart: "Pending " + token);

        // search: name (any case), email, phone
        Ids(await List(admin, "&search=" + token.ToUpperInvariant())).Should().BeEquivalentTo(new[] { active.Id, pending.Id });
        Ids(await List(admin, "&search=" + Uri.EscapeDataString(active.Email))).Should().BeEquivalentTo(new[] { active.Id });
        Ids(await List(admin, "&search=" + active.Phone)).Should().BeEquivalentTo(new[] { active.Id });

        // status (snake_case or PascalCase), combined with the search
        Ids(await List(admin, $"&search={token}&status=active")).Should().BeEquivalentTo(new[] { active.Id });
        Ids(await List(admin, $"&search={token}&status=pending_verification")).Should().BeEquivalentTo(new[] { pending.Id });
        Ids(await List(admin, $"&search={token}&status=PendingVerification")).Should().BeEquivalentTo(new[] { pending.Id });

        foreach (var bad in new[] { "&type=bogus", "&type=1", "&status=bogus", "&status=1" })
        {
            var r = await admin.Client.GetAsync("/admin/api/users?pageSize=100" + bad);
            (await ErrorCode(r)).Should().Be("INVALID_VALUE", bad);
        }
    }

    [Fact]
    public async Task List_paginates_newest_first_with_a_correct_total()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.view");
        var token = Guid.NewGuid().ToString("N")[..10];
        var a = await NewIndividual(namePart: "Page A " + token);
        var b = await NewIndividual(namePart: "Page B " + token);
        var c = await NewIndividual(namePart: "Page C " + token);

        var page1 = await admin.Client.GetAsync($"/admin/api/users?search={token}&pageSize=2&page=1");
        var page2 = await admin.Client.GetAsync($"/admin/api/users?search={token}&pageSize=2&page=2");
        var root1 = await Json(page1);
        var root2 = await Json(page2);

        root1.GetProperty("meta").GetProperty("total").GetInt32().Should().Be(3);
        root1.GetProperty("meta").GetProperty("totalPages").GetInt32().Should().Be(2);
        Ids(root1.GetProperty("data")).Should().Equal(c.Id, b.Id);   // newest first
        Ids(root2.GetProperty("data")).Should().Equal(a.Id);
    }

    // ------------------------------------------------------------------ detail
    [Fact]
    public async Task Detail_shows_the_company_profile_for_a_company_and_null_for_an_individual_with_deal_counts()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.view");
        var company = await NewCompany();
        var buyer = await NewIndividual();
        var seller = await NewIndividual();
        await CreateDeal(buyer, seller);

        var companyDetail = await Data(await admin.Client.GetAsync($"/admin/api/users/{company.Id}"));
        companyDetail.GetProperty("type").GetString().Should().Be("company");
        var profile = companyDetail.GetProperty("companyProfile");
        profile.GetProperty("companyName").GetString().Should().Be(company.Name);
        profile.GetProperty("commercialRegNo").GetString().Should().StartWith("CRAU");
        profile.GetProperty("taxNo").GetString().Should().StartWith("TAX");
        profile.GetProperty("ownerPhone").GetString().Should().Be(company.Phone);

        var buyerDetail = await Data(await admin.Client.GetAsync($"/admin/api/users/{buyer.Id}"));
        buyerDetail.GetProperty("companyProfile").ValueKind.Should().Be(JsonValueKind.Null);
        buyerDetail.GetProperty("dealsAsBuyer").GetInt32().Should().Be(1);
        buyerDetail.GetProperty("dealsAsSeller").GetInt32().Should().Be(0);
        foreach (var field in new[] { "language", "phoneVerifiedAt", "emailVerifiedAt", "lastLoginAt", "status", "nafathVerified", "createdAt" })
            buyerDetail.TryGetProperty(field, out _).Should().BeTrue(field);

        var sellerDetail = await Data(await admin.Client.GetAsync($"/admin/api/users/{seller.Id}"));
        sellerDetail.GetProperty("dealsAsBuyer").GetInt32().Should().Be(0);
        sellerDetail.GetProperty("dealsAsSeller").GetInt32().Should().Be(1);

        (await ErrorCode(await admin.Client.GetAsync("/admin/api/users/999999999"))).Should().Be("USER_NOT_FOUND");
    }

    // ------------------------------------------------------------------ delete
    [Fact]
    public async Task Delete_soft_deletes_hides_the_user_blocks_login_revokes_sessions_and_writes_an_audit_row()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.view", "admin.users.manage");
        var user = await NewIndividual();
        (await Db(db => db.RefreshTokens.AsNoTracking().CountAsync(t => t.UserId == user.Id && t.RevokedAt == null))).Should().BeGreaterThan(0);

        var delete = await admin.Client.DeleteAsync($"/admin/api/users/{user.Id}");
        (await Succeeded(delete)).Should().BeTrue(await delete.Content.ReadAsStringAsync());

        // soft: the row is still there
        var row = await Db(db => db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => new { u.Status, u.DeletedAt }).FirstAsync());
        row.Status.Should().Be(AccountStatus.Deleted);
        row.DeletedAt.Should().NotBeNull();

        // hidden from detail and list
        (await ErrorCode(await admin.Client.GetAsync($"/admin/api/users/{user.Id}"))).Should().Be("USER_NOT_FOUND");
        Ids(await List(admin, "&search=" + user.Phone)).Should().NotContain(user.Id);

        // login blocked: the login query filters DeletedAt == null, so a deleted user is "not registered"
        // (Claude's draft expected INVALID_CREDENTIALS here — that is wrong for a deleted user.)
        var login = await _anon.PostAsJsonAsync("/api/v1/auth/login", new { phone = user.Phone, password = Password });
        (await Succeeded(login)).Should().BeFalse();
        (await ErrorCode(login)).Should().Be("PHONE_NOT_REGISTERED");

        // app sessions revoked
        (await Db(db => db.RefreshTokens.AsNoTracking().CountAsync(t => t.UserId == user.Id && t.RevokedAt == null))).Should().Be(0);

        // audit row with before / after
        var audit = await Db(db => db.AuditLogs.AsNoTracking().Where(a => a.Action == "admin.user.delete" && a.EntityId == user.Id).ToListAsync());
        audit.Should().ContainSingle();
        audit[0].EntityType.Should().Be("User");
        audit[0].ActorId.Should().Be(admin.AdminId);
        audit[0].OldValues.Should().Contain("Active");
        audit[0].NewValues.Should().Contain("Deleted");

        // a second delete is a 404-style error
        (await ErrorCode(await admin.Client.DeleteAsync($"/admin/api/users/{user.Id}"))).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_is_blocked_while_the_user_has_an_unfinished_deal_and_allowed_once_it_is_rejected()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.manage");
        var buyer = await NewIndividual();
        var seller = await NewIndividual();
        var dealId = await CreateDeal(buyer, seller);   // pending_acceptance: not finished

        foreach (var party in new[] { buyer, seller })
        {
            var blocked = await admin.Client.DeleteAsync($"/admin/api/users/{party.Id}");
            (await ErrorCode(blocked)).Should().Be("USER_HAS_ACTIVE_DEALS");
        }
        (await Db(db => db.Users.AsNoTracking().Where(u => u.Id == buyer.Id).Select(u => u.Status).FirstAsync())).Should().Be(AccountStatus.Active);
        (await Db(db => db.AuditLogs.AsNoTracking().CountAsync(a => a.Action == "admin.user.delete" && a.EntityId == buyer.Id))).Should().Be(0);

        // the buyer rejects: Rejected is a finished status
        var reject = await buyer.Client!.PostAsJsonAsync($"/api/v1/deals/{dealId}/reject", new { reasonId = 1, note = "no" });
        (await Succeeded(reject)).Should().BeTrue(await reject.Content.ReadAsStringAsync());

        (await Succeeded(await admin.Client.DeleteAsync($"/admin/api/users/{buyer.Id}"))).Should().BeTrue();
        // the deal itself is untouched
        (await Db(db => db.Deals.AsNoTracking().CountAsync(d => d.Id == dealId))).Should().Be(1);
    }

    [Fact]
    public async Task Delete_is_blocked_while_the_user_has_an_unfinished_company_deal()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.manage");
        var creator = await NewCompany();
        var counterparty = await NewIndividual();
        var dealId = await CreateCompanyDealDraft(creator, counterparty);   // Draft: not finished

        // both sides are blocked, creator and counterparty alike
        foreach (var party in new[] { creator, counterparty })
        {
            var blocked = await admin.Client.DeleteAsync($"/admin/api/users/{party.Id}");
            (await ErrorCode(blocked)).Should().Be("USER_HAS_ACTIVE_DEALS");
        }
        (await Db(db => db.Users.AsNoTracking().Where(u => u.Id == creator.Id).Select(u => u.Status).FirstAsync())).Should().Be(AccountStatus.Active);
        (await Db(db => db.AuditLogs.AsNoTracking().CountAsync(a => a.Action == "admin.user.delete" && a.EntityId == creator.Id))).Should().Be(0);

        // submit + accept: Active is still not finished
        var submit = await creator.Client!.PostAsJsonAsync($"/api/v1/company-deals/{dealId}/submit", new { });
        (await Succeeded(submit)).Should().BeTrue(await submit.Content.ReadAsStringAsync());
        var accept = await counterparty.Client!.PostAsJsonAsync($"/api/v1/company-deals/{dealId}/accept", new { acceptTerms = true });
        (await Succeeded(accept)).Should().BeTrue(await accept.Content.ReadAsStringAsync());

        var stillBlocked = await admin.Client.DeleteAsync($"/admin/api/users/{creator.Id}");
        (await ErrorCode(stillBlocked)).Should().Be("USER_HAS_ACTIVE_DEALS");
    }

    [Fact]
    public async Task Delete_is_allowed_once_the_company_deal_is_rejected()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.manage");
        var creator = await NewCompany();
        var counterparty = await NewIndividual();
        var dealId = await CreateCompanyDealDraft(creator, counterparty);

        var submit = await creator.Client!.PostAsJsonAsync($"/api/v1/company-deals/{dealId}/submit", new { });
        (await Succeeded(submit)).Should().BeTrue(await submit.Content.ReadAsStringAsync());
        // reason 13 is a company_reject_reason seed
        var reject = await counterparty.Client!.PostAsJsonAsync($"/api/v1/company-deals/{dealId}/reject", new { reasonId = 13, note = "no" });
        (await Succeeded(reject)).Should().BeTrue(await reject.Content.ReadAsStringAsync());

        (await Succeeded(await admin.Client.DeleteAsync($"/admin/api/users/{creator.Id}"))).Should().BeTrue();
        // the company deal itself is untouched
        (await Db(db => db.CompanyDeals.AsNoTracking().CountAsync(d => d.Id == dealId))).Should().Be(1);
    }

    [Fact]
    public async Task Deleting_a_company_keeps_its_company_profile()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.manage");
        var company = await NewCompany();

        (await Succeeded(await admin.Client.DeleteAsync($"/admin/api/users/{company.Id}"))).Should().BeTrue();
        (await Db(db => db.CompanyProfiles.AsNoTracking().CountAsync(c => c.UserId == company.Id))).Should().Be(1);
    }

    // ------------------------------------------------------------------ gates
    [Fact]
    public async Task Gates_no_token_and_app_user_token_are_unauthorized_and_missing_permission_is_forbidden()
    {
        var target = await NewIndividual();
        var appUser = await NewIndividual();
        var noPermission = await AdminTestKit.CreateAsync(factory);
        var viewOnly = await AdminTestKit.CreateAsync(factory, "admin.users.view");
        var manageOnly = await AdminTestKit.CreateAsync(factory, "admin.users.manage");

        var calls = new (string Name, Func<HttpClient, Task<HttpResponseMessage>> Call)[]
        {
            ("list", c => c.GetAsync("/admin/api/users")),
            ("detail", c => c.GetAsync($"/admin/api/users/{target.Id}")),
            ("delete", c => c.DeleteAsync($"/admin/api/users/{target.Id}"))
        };
        foreach (var (name, call) in calls)
        {
            (await ErrorCode(await call(_anon))).Should().Be("UNAUTHORIZED", "no token: " + name);
            (await ErrorCode(await call(appUser.Client!))).Should().Be("UNAUTHORIZED", "app-user token: " + name);
            (await ErrorCode(await call(noPermission.Client))).Should().Be("ADMIN_FORBIDDEN", "no permission: " + name);
        }

        // the two permissions are separate
        (await ErrorCode(await viewOnly.Client.DeleteAsync($"/admin/api/users/{target.Id}"))).Should().Be("ADMIN_FORBIDDEN");
        (await ErrorCode(await manageOnly.Client.GetAsync("/admin/api/users"))).Should().Be("ADMIN_FORBIDDEN");
        (await ErrorCode(await manageOnly.Client.GetAsync($"/admin/api/users/{target.Id}"))).Should().Be("ADMIN_FORBIDDEN");

        // nothing was deleted by the refused calls
        (await Db(db => db.Users.AsNoTracking().Where(u => u.Id == target.Id).Select(u => u.Status).FirstAsync())).Should().Be(AccountStatus.Active);
    }
}
