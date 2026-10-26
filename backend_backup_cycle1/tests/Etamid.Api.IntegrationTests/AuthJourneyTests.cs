using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

public class AuthJourneyTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private readonly HttpClient _client = factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static string Code(JsonElement body) => body.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private Task<HttpResponseMessage> Post(string url, object body, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (bearer is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> Register(string local, string email)
        => Post("/api/v1/auth/register/individual",
            new { username = "Ahmed", phone = local, email, password = Password, acceptTerms = true });

    private static string FakeToken(string e164) => $"fake:{e164}";

    [Fact]
    public async Task Full_journey_register_verify_me_refresh_and_reuse_detection()
    {
        var reg = await Register("0501110001", "a1@example.com");
        reg.StatusCode.Should().Be(HttpStatusCode.Created);

        // Not verified yet
        var early = await Post("/api/v1/auth/login", new { phone = "0501110001", password = Password });
        early.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Code(await Json(early)).Should().Be("ACCOUNT_NOT_VERIFIED");

        // Verify with Firebase (fake)
        var verify = await Post("/api/v1/auth/phone/verify",
            new { phone = "0501110001", firebaseIdToken = FakeToken("+966501110001") });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await Json(verify)).GetProperty("data");
        var access = data.GetProperty("accessToken").GetString()!;
        var refresh = data.GetProperty("refreshToken").GetString()!;

        // /me
        var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var me = await _client.SendAsync(meReq);
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        // Refresh rotates
        var rotated = await Post("/api/v1/auth/refresh", new { refreshToken = refresh });
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-using the old refresh token is theft: rejected
        var reused = await Post("/api/v1/auth/refresh", new { refreshToken = refresh });
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Code(await Json(reused)).Should().Be("REFRESH_REUSED");

        // Normal login now works
        var login = await Post("/api/v1/auth/login", new { phone = "0501110001", password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Duplicate_verified_phone_is_a_409()
    {
        (await Register("0501110002", "b1@example.com")).StatusCode.Should().Be(HttpStatusCode.Created);
        await Post("/api/v1/auth/phone/verify", new { phone = "0501110002", firebaseIdToken = FakeToken("+966501110002") });

        var dup = await Register("0501110002", "b2@example.com");
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Code(await Json(dup)).Should().Be("PHONE_EXISTS");
    }

    [Fact]
    public async Task Registering_again_before_verifying_is_allowed()
    {
        (await Register("0501110007", "g1@example.com")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Register("0501110007", "g2@example.com")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Verifying_with_another_phones_token_is_rejected()
    {
        await Register("0501110003", "c1@example.com");
        var r = await Post("/api/v1/auth/phone/verify",
            new { phone = "0501110003", firebaseIdToken = FakeToken("+966509999999") });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Code(await Json(r)).Should().Be("PHONE_TOKEN_INVALID");
    }

    [Fact]
    public async Task Weak_password_is_a_422_with_a_field()
    {
        var r = await Post("/api/v1/auth/register/individual",
            new { username = "A", phone = "0501110004", email = "d@example.com", password = "abcdef", acceptTerms = true });
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var err = (await Json(r)).GetProperty("errors")[0];
        err.GetProperty("code").GetString().Should().Be("WEAK_PASSWORD");
        err.GetProperty("field").GetString().Should().Be("password");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        await Register("0501110005", "e1@example.com");
        await Post("/api/v1/auth/phone/verify", new { phone = "0501110005", firebaseIdToken = FakeToken("+966501110005") });

        for (var i = 0; i < 5; i++)
            await Post("/api/v1/auth/login", new { phone = "0501110005", password = "Wrong#999" });

        var locked = await Post("/api/v1/auth/login", new { phone = "0501110005", password = Password });
        locked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        Code(await Json(locked)).Should().Be("ACCOUNT_LOCKED");
        locked.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task Reset_password_with_a_fresh_firebase_token()
    {
        await Register("0501110006", "f1@example.com");
        await Post("/api/v1/auth/phone/verify", new { phone = "0501110006", firebaseIdToken = FakeToken("+966501110006") });
        await Task.Delay(1100); // fake auth_time must be newer than the one used for verification

        var reset = await Post("/api/v1/auth/password/reset", new
        {
            phone = "0501110006",
            firebaseIdToken = FakeToken("+966501110006"),
            newPassword = "New#4567",
            confirmPassword = "New#4567"
        });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Post("/api/v1/auth/login", new { phone = "0501110006", password = "New#4567" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_without_token_is_401_with_the_envelope()
    {
        var r = await _client.GetAsync("/api/v1/me");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Code(await Json(r)).Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Verify_with_otp_endpoint_succeeds_and_allows_login()
    {
        var reg = await Register("0501110099", "otp1@example.com");
        reg.StatusCode.Should().Be(HttpStatusCode.Created);

        var verify = await Post("/api/v1/auth/otp/verify", new { phone = "0501110099", code = "123456" });
        verify.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await Post("/api/v1/auth/login", new { phone = "0501110099", password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resend_otp_endpoint_returns_ok()
    {
        await Register("0501110098", "otp2@example.com");
        var resend = await Post("/api/v1/auth/otp/resend", new { phone = "0501110098", purpose = "register" });
        resend.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
