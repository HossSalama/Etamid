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

    private Task<HttpResponseMessage> Post(string url, object? body, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        if (bearer is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> Register(string local, string email)
        => Post("/api/v1/auth/register/individual",
            new { username = "Ahmed", phone = local, email, password = Password, acceptTerms = true });

    private Task<HttpResponseMessage> VerifyEmail(string local, string code)
        => Post("/api/v1/auth/otp/verify", new { phone = local, code });

    /// <summary>Registers and activates an account with the emailed code.</summary>
    private async Task<JsonElement> RegisterAndVerify(string local, string email)
    {
        (await Register(local, email)).StatusCode.Should().Be(HttpStatusCode.Created);
        var verify = await VerifyEmail(local, factory.Emails.CodeFor(email));
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await Json(verify)).GetProperty("data");
    }

    private static string WrongCode(string real) => real == "000000" ? "111111" : "000000";

    [Fact]
    public async Task Full_journey_register_verify_email_me_refresh_and_reuse_detection()
    {
        var reg = await Register("0501110001", "a1@example.com");
        reg.StatusCode.Should().Be(HttpStatusCode.Created);
        var regData = (await Json(reg)).GetProperty("data");
        regData.GetProperty("verificationMethod").GetString().Should().Be("email");
        regData.GetProperty("verificationRequired").GetBoolean().Should().BeTrue();

        // Not activated yet
        var early = await Post("/api/v1/auth/login", new { phone = "0501110001", password = Password });
        early.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Code(await Json(early)).Should().Be("ACCOUNT_NOT_VERIFIED");

        var real = factory.Emails.CodeFor("a1@example.com");
        var wrong = await VerifyEmail("0501110001", WrongCode(real));
        wrong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Code(await Json(wrong)).Should().Be("OTP_INVALID");

        var verify = await VerifyEmail("0501110001", real);
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await Json(verify)).GetProperty("data");
        var access = data.GetProperty("accessToken").GetString()!;
        var refresh = data.GetProperty("refreshToken").GetString()!;
        data.GetProperty("user").GetProperty("emailVerified").GetBoolean().Should().BeTrue();

        var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        (await _client.SendAsync(meReq)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Post("/api/v1/auth/refresh", new { refreshToken = refresh })).StatusCode.Should().Be(HttpStatusCode.OK);

        var reused = await Post("/api/v1/auth/refresh", new { refreshToken = refresh });
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Code(await Json(reused)).Should().Be("REFRESH_REUSED");

        (await Post("/api/v1/auth/login", new { phone = "0501110001", password = Password })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_code_works_once()
    {
        await Register("0501110008", "h1@example.com");
        var code = factory.Emails.CodeFor("h1@example.com");

        (await VerifyEmail("0501110008", code)).StatusCode.Should().Be(HttpStatusCode.OK);
        var again = await VerifyEmail("0501110008", code);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Code(await Json(again)).Should().Be("ACCOUNT_ALREADY_VERIFIED");
    }

    [Fact]
    public async Task Duplicate_verified_phone_is_a_409()
    {
        await RegisterAndVerify("0501110002", "b1@example.com");

        var dup = await Register("0501110002", "b2@example.com");
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Code(await Json(dup)).Should().Be("PHONE_EXISTS");
    }

    [Fact]
    public async Task Changing_email_before_resend_cooldown_is_rejected_and_original_code_remains_valid()
    {
        (await Register("0501110007", "g1@example.com")).StatusCode.Should().Be(HttpStatusCode.Created);
        var retry = await Register("0501110007", "g2@example.com");
        retry.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        Code(await Json(retry)).Should().Be("OTP_COOLDOWN");

        var verify = await VerifyEmail("0501110007", factory.Emails.CodeFor("g1@example.com"));
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resending_during_cooldown_returns_generic_success_without_sending()
    {
        await Register("0501110009", "i1@example.com");
        var before = factory.Emails.SentCount;

        var resend = await Post("/api/v1/auth/otp/resend", new { phone = "0501110009" });
        resend.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Emails.SentCount.Should().Be(before);
    }

    [Fact]
    public async Task Resend_for_an_unknown_phone_looks_like_success()
    {
        var before = factory.Emails.SentCount;
        (await Post("/api/v1/auth/otp/resend", new { phone = "0501119999" })).StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Emails.SentCount.Should().Be(before);
    }

    [Fact]
    public async Task Five_wrong_codes_kill_the_code_even_for_the_right_one()
    {
        await Register("0501110010", "j1@example.com");
        var real = factory.Emails.CodeFor("j1@example.com");

        for (var i = 0; i < 5; i++)
            (await VerifyEmail("0501110010", WrongCode(real))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var locked = await VerifyEmail("0501110010", real);
        locked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        Code(await Json(locked)).Should().Be("OTP_ATTEMPTS_EXCEEDED");
    }

    [Fact]
    public async Task Malformed_code_is_a_422()
    {
        await Register("0501110011", "k1@example.com");
        var r = await VerifyEmail("0501110011", "12ab");
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Code(await Json(r)).Should().Be("OTP_INVALID");
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
        await RegisterAndVerify("0501110005", "e1@example.com");

        for (var i = 0; i < 5; i++)
            await Post("/api/v1/auth/login", new { phone = "0501110005", password = "Wrong#999" });

        var locked = await Post("/api/v1/auth/login", new { phone = "0501110005", password = Password });
        locked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        Code(await Json(locked)).Should().Be("ACCOUNT_LOCKED");
        locked.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task Forgot_password_three_steps_then_login_with_the_new_password()
    {
        await RegisterAndVerify("0501110006", "f1@example.com");

        (await Post("/api/v1/auth/password/forgot", new { phone = "0501110006" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var code = factory.Emails.CodeFor("f1@example.com");

        var wrong = await Post("/api/v1/auth/password/verify-otp", new { phone = "0501110006", code = WrongCode(code) });
        wrong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var verified = await Post("/api/v1/auth/password/verify-otp", new { phone = "0501110006", code });
        verified.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await Json(verified)).GetProperty("data").GetProperty("resetToken").GetString()!;

        var reset = await Post("/api/v1/auth/password/reset",
            new { resetToken = token, newPassword = "New#4567", confirmPassword = "New#4567" });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Post("/api/v1/auth/login", new { phone = "0501110006", password = "New#4567" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post("/api/v1/auth/login", new { phone = "0501110006", password = Password })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The token is single-use.
        var again = await Post("/api/v1/auth/password/reset",
            new { resetToken = token, newPassword = "Other#4567", confirmPassword = "Other#4567" });
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Code(await Json(again)).Should().Be("RESET_TOKEN_INVALID");
    }

    [Fact]
    public async Task Forgot_password_never_reveals_whether_the_phone_exists()
    {
        var before = factory.Emails.SentCount;
        var r = await Post("/api/v1/auth/password/forgot", new { phone = "0501118888" });
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Emails.SentCount.Should().Be(before);
    }

    [Fact]
    public async Task Reset_with_a_made_up_token_is_rejected()
    {
        var r = await Post("/api/v1/auth/password/reset",
            new { resetToken = "not-a-real-token", newPassword = "New#4567", confirmPassword = "New#4567" });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Code(await Json(r)).Should().Be("RESET_TOKEN_INVALID");
    }

    [Fact]
    public async Task Me_without_token_is_401_with_the_envelope()
    {
        var r = await _client.GetAsync("/api/v1/me");
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Code(await Json(r)).Should().Be("UNAUTHORIZED");
    }
}
