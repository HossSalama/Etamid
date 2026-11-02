using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Commands;
using Etamid.Infrastructure.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// SuccessfulDeals: a released deal counts once for the seller, whether the
/// release was confirmed by the buyer or automatic, and a rating on top of a
/// released deal must not count it a second time.
/// Note: the rating test assumes the reviews feature (POST /deals/{id}/rating)
/// is already applied, as it is on the user's tree.
/// </summary>
public class SuccessfulDealsTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 800;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05066{n:D5}";
        var email = $"deals.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Deals User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private async Task<long> FundedDeal(Party buyer, Party seller)
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
        return id;
    }

    private async Task<long> InspectionDeal(Party buyer, Party seller)
    {
        var id = await FundedDeal(buyer, seller);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<long> ReleasedDeal(Party buyer, Party seller, string? key = null)
    {
        var id = await InspectionDeal(buyer, seller);
        var release = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, key ?? Key());
        release.StatusCode.Should().Be(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        return id;
    }

    private static async Task<int> LookupSuccessfulDeals(Party who, string sellerPhone)
    {
        var lookup = await Json(await who.Client.GetAsync($"/api/v1/users/lookup?q={sellerPhone}"));
        return lookup.GetProperty("data").GetProperty("successfulDeals").GetInt32();
    }

    [Fact]
    public async Task Manual_release_counts_one_successful_deal_for_seller()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        (await LookupSuccessfulDeals(buyer, seller.Phone)).Should().Be(0);
        await ReleasedDeal(buyer, seller);
        (await LookupSuccessfulDeals(buyer, seller.Phone)).Should().Be(1);
    }

    [Fact]
    public async Task Auto_release_counts_one_successful_deal_for_seller()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller);

        // Auto-release is only legal after the inspection window ends (24h for
        // this deal), so the test clock must move past it first — an immediate
        // auto-release is correctly refused with DEAL_INSPECTION_NOT_ENDED.
        // Reset afterwards so later tests in this class see real time again.
        factory.Clock.Advance(TimeSpan.FromHours(25));
        try
        {
            using var scope = factory.Services.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new AutoReleaseDealCommand(id));
            result.IsSuccess.Should().BeTrue(result.Error?.Code ?? "no error");
        }
        finally
        {
            factory.Clock.Reset();
        }

        (await LookupSuccessfulDeals(buyer, seller.Phone)).Should().Be(1);
    }

    [Fact]
    public async Task Replayed_release_does_not_count_twice()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller);
        var key = Key();

        (await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, key)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, key)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await LookupSuccessfulDeals(buyer, seller.Phone)).Should().Be(1);
    }

    [Fact]
    public async Task Rating_a_released_deal_does_not_count_it_twice()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await ReleasedDeal(buyer, seller);

        (await LookupSuccessfulDeals(buyer, seller.Phone)).Should().Be(1);
        (await Post(buyer.Client, $"/api/v1/deals/{id}/rating", new { stars = 5 })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await LookupSuccessfulDeals(buyer, seller.Phone)).Should().Be(1);
    }
}
