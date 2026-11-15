using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

public class RegistrationRefreshTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const string NewPassword = "NewPass#456";
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static string Code(JsonElement body) => body.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private Task<HttpResponseMessage> Post(string url, object? body, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        if (bearer is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> Register(string local, string email, string password = Password)
        => Post("/api/v1/auth/register/individual",
            new { username = "Ahmed", phone = local, email, password, acceptTerms = true });

    private Task<HttpResponseMessage> VerifyEmail(string local, string code)
        => Post("/api/v1/auth/otp/verify", new { phone = local, code });

    private Task<HttpResponseMessage> Login(string local, string password)
        => Post("/api/v1/auth/login", new { phone = local, password });

    /// <summary>
    /// Re-registering a pending account with the same phone refreshes it
    /// (new password, new code) instead of PHONE_EXISTS.
    /// </summary>
    [Fact]
    public async Task Re_registering_with_the_same_phone_refreshes_the_pending_account()
    {
        var first = await Register("0501120001", "r1@example.com");
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await Register("0501120001", "r1@example.com", NewPassword);
        again.StatusCode.Should().Be(HttpStatusCode.Created);

        var verify = await VerifyEmail("0501120001", factory.Emails.CodeFor("r1@example.com"));
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await Login("0501120001", NewPassword);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(login)).GetProperty("data").GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Re-registering a pending account with the same email but a new phone
    /// refreshes it (owner decision 2026-11-15): the phone moves to the new
    /// number and a fresh code goes to the email, instead of EMAIL_EXISTS.
    /// </summary>
    [Fact]
    public async Task Re_registering_with_the_same_email_but_a_new_phone_refreshes_the_pending_account()
    {
        var first = await Register("0501120002", "r2@example.com");
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await Register("0501120003", "r2@example.com", NewPassword);
        again.StatusCode.Should().Be(HttpStatusCode.Created);

        var verify = await VerifyEmail("0501120003", factory.Emails.CodeFor("r2@example.com"));
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var oldPhone = await Login("0501120002", NewPassword);
        oldPhone.StatusCode.Should().Be(HttpStatusCode.OK);
        Code(await Json(oldPhone)).Should().Be("PHONE_NOT_REGISTERED");

        var login = await Login("0501120003", NewPassword);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(login)).GetProperty("data").GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// A verified account still says it exists on re-register, by phone or by email.
    /// </summary>
    [Fact]
    public async Task Re_registering_a_verified_account_still_says_it_exists()
    {
        var reg = await Register("0501120004", "r3@example.com");
        reg.StatusCode.Should().Be(HttpStatusCode.Created);
        var verify = await VerifyEmail("0501120004", factory.Emails.CodeFor("r3@example.com"));
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var samePhone = await Register("0501120004", "r3@example.com");
        samePhone.StatusCode.Should().Be(HttpStatusCode.OK);
        Code(await Json(samePhone)).Should().Be("PHONE_EXISTS");

        var sameEmail = await Register("0501120005", "r3@example.com");
        sameEmail.StatusCode.Should().Be(HttpStatusCode.OK);
        Code(await Json(sameEmail)).Should().Be("EMAIL_EXISTS");
    }
}
