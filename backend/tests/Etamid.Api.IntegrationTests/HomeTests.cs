using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>GET /api/v1/home: one call for the mobile home screen (user + wallet + active deals).</summary>
public class HomeTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 7000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone, string Name);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser(string name)
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05071{n:D5}";
        var email = $"home.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = name, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local, name);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        return await c.SendAsync(request);
    }

    private static async Task<long> CreateDeal(Party buyer, Party seller, decimal amount = 1000m)
    {
        var r = await Post(seller.Client, "/api/v1/deals", new
        {
            title = "Used phone", buyerPhoneOrCode = buyer.Phone, description = "iPhone in good condition",
            amount, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private static async Task SaveStore(Party seller, string storeName)
    {
        var lookup = await Json(await seller.Client.GetAsync("/api/v1/lookups/store-categories"));
        var categoryId = lookup.GetProperty("data")[0].GetProperty("id").GetInt64();
        var save = await seller.Client.PutAsJsonAsync("/api/v1/me/store",
            new { storeName, categoryId, bannerFileId = (string?)null });
        save.StatusCode.Should().Be(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> Home(Party who)
    {
        var r = await who.Client.GetAsync("/api/v1/home");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return await Json(r);
    }

    // ------------------------------------------------------------------ tests
    [Fact]
    public async Task Unauthenticated_returns_unauthorized_envelope()
    {
        var r = await _anon.GetAsync("/api/v1/home");

        // Auth challenges carry no body, so the errors-200 status-pages rule
        // rewrites them to HTTP 200 with UNAUTHORIZED in the envelope.
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Json(r);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Empty_home_for_new_user()
    {
        var user = await NewUser("Home User");

        var body = await Home(user);
        var data = body.GetProperty("data");

        data.GetProperty("user").GetProperty("name").GetString().Should().Be("Home User");
        data.GetProperty("user").GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        var wallet = data.GetProperty("wallet");
        wallet.GetProperty("activated").GetBoolean().Should().BeFalse();
        wallet.GetProperty("availableBalance").ValueKind.Should().Be(JsonValueKind.Null);
        wallet.GetProperty("escrowHeld").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("activeDeals").GetArrayLength().Should().Be(0);
        data.GetProperty("activeDealsCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Home_returns_wallet_and_active_deal_with_store_name()
    {
        var buyer = await NewUser("Home Buyer");
        var seller = await NewUser("Home Seller");
        await SaveStore(seller, "Nabila Shop");
        var dealId = await CreateDeal(buyer, seller, amount: 6200m);
        (await Post(buyer.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Home(buyer);
        var data = body.GetProperty("data");

        var wallet = data.GetProperty("wallet");
        wallet.GetProperty("activated").GetBoolean().Should().BeTrue();
        wallet.GetProperty("availableBalance").GetDecimal().Should().Be(0m);
        wallet.GetProperty("escrowHeld").GetDecimal().Should().Be(0m);

        data.GetProperty("activeDealsCount").GetInt32().Should().Be(1);
        var deal = data.GetProperty("activeDeals")[0];
        deal.GetProperty("id").GetInt64().Should().Be(dealId);
        deal.GetProperty("title").GetString().Should().Be("Used phone");
        deal.GetProperty("amount").GetDecimal().Should().Be(6200m);
        deal.GetProperty("status").GetString().Should().Be("pending_acceptance");
        deal.GetProperty("role").GetString().Should().Be("buyer");
        deal.GetProperty("counterpartyName").GetString().Should().Be("Home Seller");
        deal.GetProperty("counterpartyStoreName").GetString().Should().Be("Nabila Shop");
    }

    [Fact]
    public async Task Rejected_deal_is_not_active()
    {
        var buyer = await NewUser("Home Buyer 2");
        var seller = await NewUser("Home Seller 2");
        var dealId = await CreateDeal(buyer, seller);

        var reject = await Post(buyer.Client, $"/api/v1/deals/{dealId}/reject", new { reasonId = 1, note = "no" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());

        var data = (await Home(buyer)).GetProperty("data");
        data.GetProperty("activeDealsCount").GetInt32().Should().Be(0);
        data.GetProperty("activeDeals").GetArrayLength().Should().Be(0);
    }
}
