using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Seller store: the public page shows everything except the seller's deals.
/// A seller with a profile returns it; a seller without one still gets a page
/// (account name, zero rating); only an unknown seller id is a 404. Deal
/// summaries carry the counterparty id so cards can link to the store, and a
/// released + rated deal is reflected in the store's rating and deal count.
/// </summary>
public class StoreTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 6000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05044{n:D5}";
        var email = $"store.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Store User " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (key is not null) request.Headers.Add("X-Idempotency-Key", key);
        return await c.SendAsync(request);
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private Task<long> UserIdOf(string phone) => Db(async db =>
        await db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync());

    private static async Task<long> StoreCategoryId(Party who)
    {
        var lookup = await Json(await who.Client.GetAsync("/api/v1/lookups/store-categories"));
        var items = lookup.GetProperty("data");
        items.GetArrayLength().Should().BeGreaterThan(0);
        return items[0].GetProperty("id").GetInt64();
    }

    private async Task<long> ReleasedDeal(Party buyer, Party seller)
    {
        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Rated item", sellerPhoneOrCode = seller.Phone, description = "Item for rating",
            amount = 500m, durationValue = 1, durationUnit = "day", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var id = (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();

        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        var providerPaymentId = (await Json(fund)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount = 500m, currency = "SAR" });
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(webhook)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        var release = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());
        release.StatusCode.Should().Be(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        return id;
    }

    [Fact]
    public async Task Store_with_profile_returns_it()
    {
        var seller = await NewUser();
        var buyer = await NewUser();
        var sellerId = await UserIdOf(seller.Phone);
        var categoryId = await StoreCategoryId(seller);

        var save = await seller.Client.PutAsJsonAsync("/api/v1/me/store", new
        {
            storeName = "Shahd's Shop", categoryId, bannerFileId = (string?)null
        });
        save.StatusCode.Should().Be(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());

        var r = await buyer.Client.GetAsync($"/api/v1/sellers/{sellerId}/store");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var data = (await Json(r)).GetProperty("data");
        data.GetProperty("sellerId").GetInt64().Should().Be(sellerId);
        data.GetProperty("storeName").GetString().Should().Be("Shahd's Shop");
        data.GetProperty("categoryId").GetInt64().Should().Be(categoryId);
        data.GetProperty("category").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("ratingAvg").GetDecimal().Should().Be(0m);
        data.GetProperty("ratingCount").GetInt32().Should().Be(0);
        data.GetProperty("successfulDeals").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Store_without_profile_returns_fallback_and_unknown_is_404()
    {
        var seller = await NewUser();
        var buyer = await NewUser();
        var sellerId = await UserIdOf(seller.Phone);

        var r = await buyer.Client.GetAsync($"/api/v1/sellers/{sellerId}/store");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var data = (await Json(r)).GetProperty("data");
        data.GetProperty("sellerName").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("storeName").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("ratingAvg").GetDecimal().Should().Be(0m);
        data.GetProperty("ratingCount").GetInt32().Should().Be(0);

        (await buyer.Client.GetAsync("/api/v1/sellers/999999999/store")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _anon.GetAsync($"/api/v1/sellers/{sellerId}/store")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deal_summary_carries_counterparty_id_and_store_reflects_rating()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var sellerId = await UserIdOf(seller.Phone);

        var dealId = await ReleasedDeal(buyer, seller);
        var rate = await Post(buyer.Client, $"/api/v1/deals/{dealId}/rating", new { stars = 5, comment = "Excellent" });
        rate.StatusCode.Should().Be(HttpStatusCode.Created, await rate.Content.ReadAsStringAsync());

        var list = await Json(await buyer.Client.GetAsync("/api/v1/deals"));
        var items = list.GetProperty("data");
        items.GetArrayLength().Should().BeGreaterThan(0);
        items[0].GetProperty("counterpartyId").GetInt64().Should().Be(sellerId);

        var store = (await Json(await buyer.Client.GetAsync($"/api/v1/sellers/{sellerId}/store"))).GetProperty("data");
        store.GetProperty("ratingAvg").GetDecimal().Should().Be(5m);
        store.GetProperty("ratingCount").GetInt32().Should().Be(1);
        store.GetProperty("successfulDeals").GetInt32().Should().Be(1);

        var reviews = (await Json(await buyer.Client.GetAsync($"/api/v1/sellers/{sellerId}/reviews"))).GetProperty("data");
        reviews.GetArrayLength().Should().Be(1);
        reviews[0].GetProperty("stars").GetInt32().Should().Be(5);
    }
}
