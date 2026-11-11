using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// The deal badge: a PendingAcceptance deal returns a badge computed from its
/// respond deadline (48h by default), on both the detail and the list responses.
/// The test clock is advanced 25h after creation, so the remaining time is a
/// deterministic ~23h ("23 hours left", yellow) instead of ~48h.
/// </summary>
public class DealBadgeTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 900;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05099{n:D5}";
        var email = $"badge.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Badge User " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local);
    }

    private static async Task<long> CreateDeal(Party buyer, Party seller)
    {
        var r = await buyer.Client.PostAsJsonAsync("/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task Pending_deal_returns_badge_on_detail_and_list()
    {
        factory.Clock.Reset();
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await CreateDeal(buyer, seller);

        // Respond window is 48h by default; move 25h forward so ~23h remain.
        factory.Clock.Advance(TimeSpan.FromHours(25));

        var detail = await buyer.Client.GetAsync($"/api/v1/deals/{dealId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK, await detail.Content.ReadAsStringAsync());
        var badge = (await Data(detail)).GetProperty("badge");
        badge.GetProperty("text").GetString().Should().Be("23 hours left");
        badge.GetProperty("color").GetString().Should().Be("yellow");
        badge.GetProperty("remainingSeconds").GetInt32().Should()
            .BeGreaterThan(22 * 3600).And.BeLessThanOrEqualTo(23 * 3600);

        var list = await buyer.Client.GetAsync("/api/v1/deals?tab=new");
        list.StatusCode.Should().Be(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());
        var items = (await Data(list)).EnumerateArray().ToList();
        items.Should().ContainSingle();
        var listBadge = items[0].GetProperty("badge");
        listBadge.GetProperty("text").GetString().Should().Be("23 hours left");
        listBadge.GetProperty("color").GetString().Should().Be("yellow");
        listBadge.GetProperty("remainingSeconds").GetInt32().Should()
            .BeGreaterThan(22 * 3600).And.BeLessThanOrEqualTo(23 * 3600);

        factory.Clock.Reset();
    }
}
