using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// GET /api/v1/me/addresses/default returns exactly one address — the user's
/// default — or null data when the user has no addresses at all.
/// </summary>
public class DefaultAddressTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 9000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05077{n:D5}";
        var email = $"defaddr.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Default Address User " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local);
    }

    private static async Task<long> AddAddress(Party who, string label, string city)
    {
        var r = await who.Client.PostAsJsonAsync("/api/v1/me/addresses", new
        {
            label,
            recipientName = "Default Address User",
            recipientPhone = who.Phone,
            city,
            district = "Al Olaya",
            street = "King Fahd Road",
            buildingNo = "12",
            postalCode = "12213",
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task No_addresses_returns_null_data()
    {
        var who = await NewUser();

        var r = await who.Client.GetAsync("/api/v1/me/addresses/default");

        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var body = await Json(r);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Returns_the_default_address()
    {
        var who = await NewUser();
        var firstId = await AddAddress(who, "البيت", "Riyadh");
        await AddAddress(who, "الشغل", "Jeddah");

        var r = await who.Client.GetAsync("/api/v1/me/addresses/default");

        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var data = (await Json(r)).GetProperty("data");
        // The first address added becomes the default automatically.
        data.GetProperty("id").GetInt64().Should().Be(firstId);
        data.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        data.GetProperty("city").GetString().Should().Be("Riyadh");
    }

    [Fact]
    public async Task Returns_the_new_default_after_switch()
    {
        var who = await NewUser();
        await AddAddress(who, "البيت", "Riyadh");
        var secondId = await AddAddress(who, "الشغل", "Jeddah");

        var set = await who.Client.PutAsync($"/api/v1/me/addresses/{secondId}/default", null);
        set.StatusCode.Should().Be(HttpStatusCode.OK, await set.Content.ReadAsStringAsync());

        var r = await who.Client.GetAsync("/api/v1/me/addresses/default");

        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var data = (await Json(r)).GetProperty("data");
        data.GetProperty("id").GetInt64().Should().Be(secondId);
        data.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        data.GetProperty("city").GetString().Should().Be("Jeddah");
    }

    [Fact]
    public async Task Unauthenticated_returns_unauthorized_envelope()
    {
        var r = await _anon.GetAsync("/api/v1/me/addresses/default");

        // Auth challenges carry no body, so the errors-200 status-pages rule
        // rewrites them to HTTP 200 with UNAUTHORIZED in the envelope.
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Json(r);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
    }
}
