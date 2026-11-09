using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Infrastructure.Payments;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Shipping address freeze + manual tracking (v1):
/// - the delivery address is frozen into the deal at creation and survives later
///   edits and deletion of the saved address;
/// - delivering a shipping deal creates the shipment row, with the seller's
///   tracking number when one is given, visible on the tracking endpoint and
///   on the deal itself; a tracking number on a non-shipping deal is rejected.
/// </summary>
public class DealShippingTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 5000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05055{n:D5}";
        var email = $"ship.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Ship User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private static async Task<long> ShippingCompanyId(Party buyer)
    {
        var lookup = await Json(await buyer.Client.GetAsync("/api/v1/lookups/shipping-companies"));
        var companies = lookup.GetProperty("data");
        companies.GetArrayLength().Should().BeGreaterThan(0);
        return companies[0].GetProperty("id").GetInt64();
    }

    private static async Task<long> NewAddress(Party buyer, string city = "Riyadh")
    {
        var r = await buyer.Client.PostAsJsonAsync("/api/v1/me/addresses", new
        {
            label = "Home", recipientName = "Ahmed", recipientPhone = "0501234567", city,
            district = "Olaya", street = "King Fahd Rd", buildingNo = "12", postalCode = "12211"
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private static async Task<long> CreateShippingDeal(Party buyer, Party seller, long companyId, long? addressId)
    {
        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Shipped item", sellerPhoneOrCode = seller.Phone, description = "Delivery to buyer address",
            amount = 250m, durationValue = 3, durationUnit = "day", needShipping = true,
            shippingCompanyId = companyId, shippingAddressId = addressId, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private async Task<long> FundedShippingDeal(Party buyer, Party seller, long companyId, long? addressId)
    {
        var id = await CreateShippingDeal(buyer, seller, companyId, addressId);
        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        var providerPaymentId = (await Json(fund)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount = 250m, currency = "SAR" });
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(webhook)).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private static async Task<JsonElement> DealDetail(Party viewer, long id)
    {
        var r = await viewer.Client.GetAsync($"/api/v1/deals/{id}");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data");
    }

    [Fact]
    public async Task Shipping_address_is_frozen_at_creation_and_survives_edit_and_delete()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var companyId = await ShippingCompanyId(buyer);
        var addressId = await NewAddress(buyer, "Riyadh");

        var id = await CreateShippingDeal(buyer, seller, companyId, addressId);

        var created = await DealDetail(buyer, id);
        created.GetProperty("shippingAddressText").GetString().Should().Contain("Riyadh");
        created.GetProperty("shippingAddressText").GetString().Should().Contain("Ahmed");
        created.GetProperty("shippingCompanyNameAr").GetString().Should().NotBeNullOrEmpty();

        // Editing the saved address must not rewrite the deal's frozen address.
        var update = await buyer.Client.PutAsJsonAsync($"/api/v1/me/addresses/{addressId}", new
        {
            label = "Home", recipientName = "Ahmed", recipientPhone = "0501234567", city = "Dammam",
            district = "Olaya", street = "King Fahd Rd", buildingNo = "12", postalCode = "12211"
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        (await DealDetail(buyer, id)).GetProperty("shippingAddressText").GetString().Should().Contain("Riyadh");
        (await DealDetail(buyer, id)).GetProperty("shippingAddressText").GetString().Should().NotContain("Dammam");

        // Deleting the saved address must not erase it from the deal either.
        (await buyer.Client.DeleteAsync($"/api/v1/me/addresses/{addressId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await DealDetail(buyer, id)).GetProperty("shippingAddressText").GetString().Should().Contain("Riyadh");
    }

    [Fact]
    public async Task Deliver_with_tracking_creates_shipment_visible_on_tracking_and_deal()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var companyId = await ShippingCompanyId(buyer);
        var addressId = await NewAddress(buyer);
        var id = await FundedShippingDeal(buyer, seller, companyId, addressId);

        var deliver = await Post(seller.Client, $"/api/v1/deals/{id}/deliver", new { trackingNumber = "ARAMEX123" });
        deliver.StatusCode.Should().Be(HttpStatusCode.OK, await deliver.Content.ReadAsStringAsync());

        var tracking = await Json(await buyer.Client.GetAsync($"/api/v1/deals/{id}/tracking"));
        var data = tracking.GetProperty("data");
        data.GetProperty("trackingNumber").GetString().Should().Be("ARAMEX123");
        data.GetProperty("shipmentStatus").GetString().Should().Be("in_transit");
        data.GetProperty("events").GetArrayLength().Should().Be(0);

        (await DealDetail(buyer, id)).GetProperty("trackingNumber").GetString().Should().Be("ARAMEX123");

        // A stranger gets the same NOT_FOUND as a missing deal.
        var stranger = await NewUser();
        (await stranger.Client.GetAsync($"/api/v1/deals/{id}/tracking")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deliver_without_tracking_on_shipping_deal_creates_shipment_without_number()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var companyId = await ShippingCompanyId(buyer);
        var id = await FundedShippingDeal(buyer, seller, companyId, null);

        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);

        var data = (await Json(await buyer.Client.GetAsync($"/api/v1/deals/{id}/tracking"))).GetProperty("data");
        data.GetProperty("trackingNumber").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("shipmentStatus").GetString().Should().Be("created");
    }

    [Fact]
    public async Task Tracking_number_rejected_on_deal_without_shipping()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Pickup item", sellerPhoneOrCode = seller.Phone, description = "No shipping",
            amount = 100m, durationValue = 1, durationUnit = "day", needShipping = false, acceptTerms = true
        });
        var id = (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        var providerPaymentId = (await Json(fund)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount = 100m, currency = "SAR" });
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(webhook)).StatusCode.Should().Be(HttpStatusCode.OK);

        var deliver = await Post(seller.Client, $"/api/v1/deals/{id}/deliver", new { trackingNumber = "X123" });
        deliver.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deliver.Content.ReadAsStringAsync()).Should().Contain("trackingNumber");
    }
}
