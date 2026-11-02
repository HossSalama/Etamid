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
/// Reviews: buyer rates the seller of a released deal (one rating per deal),
/// the seller's store aggregates update, and the reviews list serves them.
/// </summary>
public class RatingTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 700;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<string> ErrorCode(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05055{n:D5}";
        var email = $"rate.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Rating User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private static async Task<long> CreateDeal(Party buyer, Party seller)
    {
        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private async Task<long> ReleasedDeal(Party buyer, Party seller)
    {
        var id = await CreateDeal(buyer, seller);
        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        var providerPaymentId = (await Json(fund)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount = 1000m, currency = "SAR" });
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(webhook)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        var release = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());
        release.StatusCode.Should().Be(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        (await Json(release)).GetProperty("data").GetProperty("status").GetString().Should().Be("released");
        return id;
    }

    private async Task<long> InspectionDeal(Party buyer, Party seller)
    {
        var id = await CreateDeal(buyer, seller);
        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        var providerPaymentId = (await Json(fund)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount = 1000m, currency = "SAR" });
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(webhook)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private Task<long> SellerIdOf(long dealId) => Db(async db =>
        await db.Deals.AsNoTracking().Where(d => d.Id == dealId).Select(d => d.SellerId).FirstAsync());

    private Task<(decimal Avg, int Count)> StoreRatingOf(long sellerId) => Db(async db =>
    {
        var store = await db.StoreProfiles.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == sellerId);
        return (store?.RatingAvg ?? 0m, store?.RatingCount ?? 0);
    });

    private static async Task<List<string>> ActionsOf(Party who, long dealId)
    {
        var list = (await Json(await who.Client.GetAsync("/api/v1/deals?page=1&pageSize=50"))).GetProperty("data");
        var deal = list.EnumerateArray().First(e => e.GetProperty("id").GetInt64() == dealId);
        return deal.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()!).ToList();
    }

    [Fact]
    public async Task Buyer_rates_released_deal_and_rate_action_disappears()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await ReleasedDeal(buyer, seller);

        (await ActionsOf(buyer, id)).Should().Contain("rate");

        var r = await Post(buyer.Client, $"/api/v1/deals/{id}/rating", new { stars = 5, comment = "Great product and fast delivery." });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var data = (await Json(r)).GetProperty("data");
        data.GetProperty("stars").GetInt32().Should().Be(5);
        data.GetProperty("comment").GetString().Should().Be("Great product and fast delivery.");

        (await StoreRatingOf(await SellerIdOf(id))).Should().Be((5m, 1));
        (await ActionsOf(buyer, id)).Should().NotContain("rate");

        var sellerId = await SellerIdOf(id);
        var reviews = await Json(await buyer.Client.GetAsync($"/api/v1/sellers/{sellerId}/reviews?page=1&pageSize=20"));
        var items = reviews.GetProperty("data");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("stars").GetInt32().Should().Be(5);
        items[0].GetProperty("dealId").GetInt64().Should().Be(id);
        items[0].GetProperty("dealTitle").GetString().Should().Be("Used phone");
        reviews.GetProperty("meta").GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Second_rating_of_same_deal_is_rejected()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await ReleasedDeal(buyer, seller);

        (await Post(buyer.Client, $"/api/v1/deals/{id}/rating", new { stars = 4 })).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await Post(buyer.Client, $"/api/v1/deals/{id}/rating", new { stars = 5 });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, await second.Content.ReadAsStringAsync());
        (await ErrorCode(second)).Should().Be("RATING_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Rating_before_release_is_rejected()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller);

        var r = await Post(buyer.Client, $"/api/v1/deals/{id}/rating", new { stars = 5 });
        r.StatusCode.Should().Be(HttpStatusCode.Conflict, await r.Content.ReadAsStringAsync());
        (await ErrorCode(r)).Should().Be("DEAL_INVALID_STATE");
    }

    [Fact]
    public async Task Seller_cannot_rate_own_deal()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await ReleasedDeal(buyer, seller);

        var r = await Post(seller.Client, $"/api/v1/deals/{id}/rating", new { stars = 5 });
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden, await r.Content.ReadAsStringAsync());
        (await ErrorCode(r)).Should().Be("DEAL_ACTION_NOT_ALLOWED");
    }

    [Fact]
    public async Task Stars_out_of_range_is_rejected()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await ReleasedDeal(buyer, seller);

        var r = await Post(buyer.Client, $"/api/v1/deals/{id}/rating", new { stars = 6 });
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await r.Content.ReadAsStringAsync());
        (await ErrorCode(r)).Should().Be("INVALID_VALUE");
    }

    [Fact]
    public async Task Store_average_is_the_mean_of_all_ratings()
    {
        var seller = await NewUser();
        var buyer1 = await NewUser();
        var buyer2 = await NewUser();
        var id1 = await ReleasedDeal(buyer1, seller);
        var id2 = await ReleasedDeal(buyer2, seller);

        (await Post(buyer1.Client, $"/api/v1/deals/{id1}/rating", new { stars = 5 })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Post(buyer2.Client, $"/api/v1/deals/{id2}/rating", new { stars = 3 })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await StoreRatingOf(await SellerIdOf(id1))).Should().Be((4m, 2));
    }

    [Fact]
    public async Task Reviews_require_authentication_and_unknown_seller_is_not_found()
    {
        var anon = await _anon.GetAsync("/api/v1/sellers/999999/reviews");
        anon.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var buyer = await NewUser();
        var r = await buyer.Client.GetAsync("/api/v1/sellers/999999/reviews");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound, await r.Content.ReadAsStringAsync());
        (await ErrorCode(r)).Should().Be("NOT_FOUND");
    }
}
