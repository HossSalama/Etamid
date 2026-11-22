using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// GET /api/v1/home/guest: the guest home screen (wallet not activated,
/// support WhatsApp, public FAQs). No login required — the iOS app calls this
/// before the user signs in.
/// </summary>
public class GuestHomeTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 8000;

    private readonly HttpClient _anon = factory.CreateClient();

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> GuestHome(HttpClient client, string? language = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/home/guest");
        if (language is not null) request.Headers.Add("Accept-Language", language);
        var r = await client.SendAsync(request);
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return await Json(r);
    }

    private async Task<HttpClient> NewUserClient(string name)
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05078{n:D5}";
        var email = $"guesthome.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = name, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ------------------------------------------------------------------ tests
    [Fact]
    public async Task Anonymous_returns_guest_home_with_wallet_not_activated()
    {
        var body = await GuestHome(_anon);

        body.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = body.GetProperty("data");
        data.GetProperty("wallet").GetProperty("activated").GetBoolean().Should().BeFalse();
        data.GetProperty("supportWhatsapp").ValueKind.Should().Be(JsonValueKind.String);
        data.GetProperty("faqs").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task SupportWhatsapp_matches_app_config()
    {
        var config = await Json(await _anon.GetAsync("/api/v1/config"));
        var expected = config.GetProperty("data").GetProperty("supportWhatsapp").GetString();

        var data = (await GuestHome(_anon)).GetProperty("data");
        data.GetProperty("supportWhatsapp").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task Faqs_match_public_faqs_endpoint()
    {
        var faqs = await Json(await _anon.GetAsync("/api/v1/support/faqs"));
        var expected = faqs.GetProperty("data");

        var data = (await GuestHome(_anon)).GetProperty("data");
        var actual = data.GetProperty("faqs");

        // Same ids/questions/answers, same order — serialized comparison so a
        // field added to one side and not the other fails loudly.
        actual.GetRawText().Should().Be(expected.GetRawText());
        actual.GetArrayLength().Should().BeGreaterThan(0); // 4 FAQs are seeded
    }

    [Fact]
    public async Task Works_with_authenticated_token_too()
    {
        var client = await NewUserClient("Guest Home User");

        var body = await GuestHome(client);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("data").GetProperty("wallet").GetProperty("activated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Faqs_localize_with_accept_language()
    {
        // No Accept-Language header -> default English (RequestLocalization
        // is configured with en + ar; default is en).
        var en = (await GuestHome(_anon)).GetProperty("data").GetProperty("faqs");
        en[0].GetProperty("question").GetString().Should().Be("What is Etamid?");

        // Accept-Language: ar -> Arabic, identical to the public FAQs
        // endpoint under the same header.
        var ar = (await GuestHome(_anon, language: "ar")).GetProperty("data").GetProperty("faqs");
        ar[0].GetProperty("question").GetString().Should().Be("ما هي منصة اعتمد؟");

        var faqsAr = await Json(await _anon.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/faqs")
            { Headers = { { "Accept-Language", "ar" } } }));
        ar.GetRawText().Should().Be(faqsAr.GetProperty("data").GetRawText());
    }
}
