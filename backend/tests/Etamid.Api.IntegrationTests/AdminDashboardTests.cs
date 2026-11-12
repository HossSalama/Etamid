using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Admin;
using Etamid.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Cycle 9 task 3 backend: audit-log ActorName, app-user counts, admin deals list.
/// </summary>
public class AdminDashboardTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 30000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone, string Email, string Name, long UserId);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static async Task<string> Code(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private static string Key() => Guid.NewGuid().ToString("N");

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (key is not null) request.Headers.Add("X-Idempotency-Key", key);
        return await c.SendAsync(request);
    }

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05077{n:D5}";
        var email = $"dash.{n}@example.com";
        var name = "Dashboard User " + n;

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = name, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code = factory.Emails.CodeFor(email) });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var userId = await Db(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync());
        return new Party(client, local, email, name, userId);
    }

    private async Task<Party> NewCompany()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05077{n:D5}";
        var email = $"dashco.{n}@example.com";
        var name = "Dashboard Company " + n;

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/company",
            new { companyName = name, ownerPhone = local, commercialRegNo = "10" + n.ToString("D8"), taxNo = (string?)null, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code = factory.Emails.CodeFor(email) });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var userId = await Db(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync());
        return new Party(client, local, email, name, userId);
    }

    private async Task<long> CreateDeal(Party buyer, Party seller, decimal amount = 500m)
    {
        var create = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Test item",
            sellerPhoneOrCode = seller.Phone,
            description = "A test item",
            amount,
            durationValue = 24,
            durationUnit = "hour",
            needShipping = false,
            acceptTerms = true
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        return (await Data(create)).GetProperty("id").GetInt64();
    }

    private async Task SendWebhook(string providerPaymentId, decimal amount)
    {
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount, currency = "SAR" });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A deal moved to Inspection (amount 500), so it has a buyer and a seller.</summary>
    private async Task<(Party Buyer, Party Seller, long DealId)> InspectionDeal()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        await SendWebhook((await Data(fund)).GetProperty("providerPaymentId").GetString()!, 500m);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        return (buyer, seller, id);
    }

    private static async Task<(long Sellers, long Buyers, long Companies, long Individuals)> Counts(HttpClient admin)
    {
        var r = await admin.GetAsync("/admin/api/users/counts");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var d = await Data(r);
        return (d.GetProperty("sellers").GetInt64(), d.GetProperty("buyers").GetInt64(),
            d.GetProperty("companies").GetInt64(), d.GetProperty("individuals").GetInt64());
    }

    // ------------------------------------------------------------------ Item 1: ActorName
    [Fact]
    public async Task AuditLog_AdminActor_ShowsAdminName()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.audit.view");
        var r = await admin.Client.GetAsync($"/admin/api/audit-logs?action=admin.login.success&actorId={admin.AdminId}&pageSize=5");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var rows = (await Data(r)).EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        rows[0].GetProperty("actorName").GetString().Should().Be("Kit Admin");
        rows[0].GetProperty("actorId").GetInt64().Should().Be(admin.AdminId);
    }

    [Fact]
    public async Task AuditLog_SystemActor_HasNullName()
    {
        var ghost = $"ghost-{Guid.NewGuid():N}@example.com";
        var login = await _anon.PostAsJsonAsync("/admin/api/auth/login", new { email = ghost, password = "Wrong#123" });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var admin = await AdminTestKit.CreateAsync(factory, "admin.audit.view");
        var r = await admin.Client.GetAsync("/admin/api/audit-logs?action=admin.login.failed&pageSize=100");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var rows = (await Data(r)).EnumerateArray().ToList();
        var row = rows.FirstOrDefault(x => (x.GetProperty("newValues").GetString() ?? "").Contains(ghost));
        row.ValueKind.Should().NotBe(JsonValueKind.Undefined, "no login.failed row for the ghost email");
        row.GetProperty("actorName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AuditLog_UserActor_ShowsUserName()
    {
        var user = await NewUser();
        await Db(async db =>
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorType = AuditActorType.User,
                ActorId = user.UserId,
                Action = "test.user_action",
                EntityType = "User",
                EntityId = user.UserId
            });
            await db.SaveChangesAsync();
            return 0;
        });

        var admin = await AdminTestKit.CreateAsync(factory, "admin.audit.view");
        var r = await admin.Client.GetAsync("/admin/api/audit-logs?action=test.user_action&pageSize=5");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var rows = (await Data(r)).EnumerateArray().ToList();
        rows.Should().NotBeEmpty();
        rows[0].GetProperty("actorName").GetString().Should().Be(user.Name);
    }

    // ------------------------------------------------------------------ Item 2: counts
    [Fact]
    public async Task UserCounts_OverlapBuyerAndSeller()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.users.view");
        var before = await Counts(admin.Client);

        var a = await NewUser();
        var b = await NewUser();
        var c = await NewUser();
        await CreateDeal(a, b); // A buys from B
        await CreateDeal(b, c); // B buys from C (B is both buyer and seller)
        _ = await NewCompany();

        var after = await Counts(admin.Client);
        (after.Sellers - before.Sellers).Should().Be(2);         // B and C
        (after.Buyers - before.Buyers).Should().Be(2);           // A and B
        (after.Companies - before.Companies).Should().Be(1);     // the company
        (after.Individuals - before.Individuals).Should().Be(3); // A, B, C
    }

    // ------------------------------------------------------------------ Item 3: deals list
    [Fact]
    public async Task DealsList_ShowsAllDeals_WithNames_AndStatusFilter()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var pendingId = await CreateDeal(buyer, seller);
        var (_, _, inspectionId) = await InspectionDeal();

        var admin = await AdminTestKit.CreateAsync(factory, "admin.deals.view");

        var all = await admin.Client.GetAsync("/admin/api/deals?pageSize=100");
        all.StatusCode.Should().Be(HttpStatusCode.OK, await all.Content.ReadAsStringAsync());
        var allRoot = await Json(all);
        var allRows = allRoot.GetProperty("data").EnumerateArray().ToList();
        allRoot.GetProperty("meta").GetProperty("total").GetInt64().Should().BeGreaterThanOrEqualTo(2);
        var pending = allRows.FirstOrDefault(x => x.GetProperty("id").GetInt64() == pendingId);
        pending.ValueKind.Should().NotBe(JsonValueKind.Undefined, "pending deal missing from the list");
        pending.GetProperty("status").GetString().Should().Be("pending_acceptance");
        pending.GetProperty("buyerName").GetString().Should().Be(buyer.Name);
        pending.GetProperty("sellerName").GetString().Should().Be(seller.Name);
        allRows.Should().Contain(x => x.GetProperty("id").GetInt64() == inspectionId);

        var filtered = await admin.Client.GetAsync("/admin/api/deals?status=pending_acceptance&pageSize=100");
        filtered.StatusCode.Should().Be(HttpStatusCode.OK, await filtered.Content.ReadAsStringAsync());
        var fRoot = await Json(filtered);
        var fRows = fRoot.GetProperty("data").EnumerateArray().ToList();
        fRoot.GetProperty("meta").GetProperty("total").GetInt64().Should().Be(fRows.Count);
        fRows.Should().Contain(x => x.GetProperty("id").GetInt64() == pendingId);
        fRows.Should().NotContain(x => x.GetProperty("id").GetInt64() == inspectionId);
        fRows.Should().OnlyContain(x => x.GetProperty("status").GetString() == "pending_acceptance");

        var insp = await admin.Client.GetAsync("/admin/api/deals?status=inspection&pageSize=100");
        insp.StatusCode.Should().Be(HttpStatusCode.OK, await insp.Content.ReadAsStringAsync());
        var iRows = (await Data(insp)).EnumerateArray().ToList();
        iRows.Should().Contain(x => x.GetProperty("id").GetInt64() == inspectionId);
        iRows.Should().NotContain(x => x.GetProperty("id").GetInt64() == pendingId);

        var bad = await admin.Client.GetAsync("/admin/api/deals?status=bogus");
        bad.StatusCode.Should().Be((HttpStatusCode)422, await bad.Content.ReadAsStringAsync());
        (await Code(bad)).Should().Be("INVALID_VALUE");
    }

    // ------------------------------------------------------------------ auth checks
    [Fact]
    public async Task NewEndpoints_Anonymous_Returns401()
    {
        // Auth challenges carry no body, so the errors-200 status-pages rule
        // rewrites them to HTTP 200 with UNAUTHORIZED in the envelope.
        var auditLogs = await _anon.GetAsync("/admin/api/audit-logs");
        auditLogs.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(auditLogs)).Should().Be("UNAUTHORIZED");
        var counts = await _anon.GetAsync("/admin/api/users/counts");
        counts.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(counts)).Should().Be("UNAUTHORIZED");
        var deals = await _anon.GetAsync("/admin/api/deals");
        deals.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(deals)).Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task NewEndpoints_AppUser_Returns401()
    {
        var user = await NewUser();
        var auditLogs = await user.Client.GetAsync("/admin/api/audit-logs");
        auditLogs.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(auditLogs)).Should().Be("UNAUTHORIZED");
        var counts = await user.Client.GetAsync("/admin/api/users/counts");
        counts.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(counts)).Should().Be("UNAUTHORIZED");
        var deals = await user.Client.GetAsync("/admin/api/deals");
        deals.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(deals)).Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task NewEndpoints_AdminWithoutPermission_Returns403()
    {
        var admin = await AdminTestKit.CreateAsync(factory); // no permissions at all
        (await admin.Client.GetAsync("/admin/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.GetAsync("/admin/api/users/counts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.GetAsync("/admin/api/deals")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
