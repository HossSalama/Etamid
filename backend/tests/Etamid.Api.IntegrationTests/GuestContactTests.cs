using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>Public guest contact: anonymous POST /support/contact-public stores a guest
/// message (never a SupportTicket), and the admin reads it under admin.support.manage.</summary>
public class GuestContactTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 31000;

    private readonly HttpClient _anon = factory.CreateClient();

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static async Task<string> Code(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        return await c.SendAsync(request);
    }

    private static object GuestBody(string name = "Nada", string channel = "nada@example.com",
        string subject = "Question about a deal", string message = "How does escrow work?")
        => new { name, channel, subject, message };

    private async Task<long> CreateGuestMessage(object? body = null)
    {
        var r = await Post(_anon, "/api/v1/support/contact-public", body ?? GuestBody());
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Data(r)).GetProperty("id").GetInt64();
    }

    private async Task<HttpClient> NewUserClient(string name)
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05073{n:D5}";
        var email = $"guest.{n}@example.com";

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
    public async Task Anonymous_create_returns_201_with_the_message()
    {
        var r = await Post(_anon, "/api/v1/support/contact-public", GuestBody());

        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var data = await Data(r);
        data.GetProperty("id").GetInt64().Should().BeGreaterThan(0);
        data.GetProperty("name").GetString().Should().Be("Nada");
        data.GetProperty("channel").GetString().Should().Be("nada@example.com");
        data.GetProperty("subject").GetString().Should().Be("Question about a deal");
        data.GetProperty("message").GetString().Should().Be("How does escrow work?");
        data.GetProperty("createdAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Create_rejects_missing_or_blank_fields()
    {
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            GuestBody(name: "")))).Should().Be("INVALID_VALUE");
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            GuestBody(channel: "   ")))).Should().Be("INVALID_VALUE");
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            new { name = "Nada", channel = "nada@example.com", message = "Hi" }))).Should().Be("INVALID_VALUE");
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            new { name = "Nada", channel = "nada@example.com", subject = "Hi" }))).Should().Be("INVALID_VALUE");
    }

    [Fact]
    public async Task Create_rejects_overlong_fields()
    {
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            GuestBody(name: new string('n', 151))))).Should().Be("TOO_LONG");
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            GuestBody(channel: new string('c', 255))))).Should().Be("TOO_LONG");
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            GuestBody(subject: new string('s', 151))))).Should().Be("TOO_LONG");
        (await Code(await Post(_anon, "/api/v1/support/contact-public",
            GuestBody(message: new string('m', 2001))))).Should().Be("TOO_LONG");
    }

    [Fact]
    public async Task Admin_list_contains_the_message_and_detail_returns_it()
    {
        var messageId = await CreateGuestMessage(GuestBody(
            name: "Guest Lister", subject: "List me, please"));
        var admin = await AdminTestKit.CreateAsync(factory, "admin.support.manage");

        // The admin list is global and the test database is shared across this
        // class, so other tests' messages are in it too — assert on our own
        // message instead of an exact global count.
        var list = await Data(await admin.Client.GetAsync(
            "/api/v1/admin/support/contact-messages?page=1&pageSize=20"));
        var mine = list.EnumerateArray().Single(m => m.GetProperty("id").GetInt64() == messageId);
        mine.GetProperty("name").GetString().Should().Be("Guest Lister");
        mine.GetProperty("subject").GetString().Should().Be("List me, please");

        var detail = await Data(await admin.Client.GetAsync(
            $"/api/v1/admin/support/contact-messages/{messageId}"));
        detail.GetProperty("id").GetInt64().Should().Be(messageId);
        detail.GetProperty("channel").GetString().Should().Be("nada@example.com");
        detail.GetProperty("message").GetString().Should().Be("How does escrow work?");

        var missing = await admin.Client.GetAsync("/api/v1/admin/support/contact-messages/999999999");
        (await Code(missing)).Should().Be("GUEST_CONTACT_NOT_FOUND");
    }

    [Fact]
    public async Task Admin_without_permission_gets_forbidden()
    {
        var admin = await AdminTestKit.CreateAsync(factory);
        await CreateGuestMessage();

        // Auth failures return HTTP 200 with the code in the envelope
        // (errors-200 rule), not a literal 403.
        var list = await admin.Client.GetAsync("/api/v1/admin/support/contact-messages");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(list)).Should().Be("ADMIN_FORBIDDEN");

        var appUser = await NewUserClient("Guest Forbidden");
        var appList = await appUser.GetAsync("/api/v1/admin/support/contact-messages");
        appList.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(appList)).Should().Be("ADMIN_FORBIDDEN");
    }
}
