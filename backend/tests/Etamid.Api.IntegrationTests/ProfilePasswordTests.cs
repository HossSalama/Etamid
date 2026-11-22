using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>PUT /me also changes the password (the edit-profile screen saves everything with one button).</summary>
public class ProfilePasswordTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const string NewPassword = "Newer#456";
    private static int _counter = 100;

    private readonly HttpClient _anon = factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static string Code(JsonElement body) => body.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private sealed record Account(HttpClient Client, string Phone, string Refresh);

    private async Task<Account> SignedIn()
    {
        var n = Interlocked.Increment(ref _counter);
        var phone = $"05088{n:D5}";
        var email = $"pw.{n}@example.com";

        await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "User " + n, phone, email, password = Password, acceptTerms = true });
        var verify = await Json(await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new { phone, code = factory.Emails.CodeFor(email) }));
        var data = verify.GetProperty("data");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString()!);
        return new Account(client, phone, data.GetProperty("refreshToken").GetString()!);
    }

    private async Task<bool> CanLogin(string phone, string password)
        => (await Json(await _anon.PostAsJsonAsync("/api/v1/auth/login", new { phone, password })))
            .GetProperty("success").GetBoolean();

    [Fact]
    public async Task Name_and_password_save_together()
    {
        var a = await SignedIn();

        var put = await Json(await a.Client.PutAsJsonAsync("/api/v1/me",
            new { username = "Nada New", currentPassword = Password, newPassword = NewPassword }));

        put.GetProperty("data").GetProperty("passwordChanged").GetBoolean().Should().BeTrue();
        (await Json(await a.Client.GetAsync("/api/v1/me"))).GetProperty("data").GetProperty("name").GetString()
            .Should().Be("Nada New");
        (await CanLogin(a.Phone, NewPassword)).Should().BeTrue();
        (await CanLogin(a.Phone, Password)).Should().BeFalse();
    }

    [Fact]
    public async Task A_wrong_current_password_rejects_the_whole_save()
    {
        var a = await SignedIn();

        var put = await Json(await a.Client.PutAsJsonAsync("/api/v1/me",
            new { username = "Should Not Save", currentPassword = "Wrong#999", newPassword = NewPassword }));

        Code(put).Should().Be("WRONG_PASSWORD");
        (await Json(await a.Client.GetAsync("/api/v1/me"))).GetProperty("data").GetProperty("name").GetString()
            .Should().NotBe("Should Not Save");
        (await CanLogin(a.Phone, Password)).Should().BeTrue();
    }

    [Fact]
    public async Task Only_one_of_the_two_password_fields_is_refused()
    {
        var a = await SignedIn();

        Code(await Json(await a.Client.PutAsJsonAsync("/api/v1/me", new { newPassword = NewPassword })))
            .Should().Be("REQUIRED");
        Code(await Json(await a.Client.PutAsJsonAsync("/api/v1/me", new { currentPassword = Password })))
            .Should().Be("REQUIRED");
    }

    [Fact]
    public async Task A_weak_or_unchanged_new_password_is_refused()
    {
        var a = await SignedIn();

        Code(await Json(await a.Client.PutAsJsonAsync("/api/v1/me",
            new { currentPassword = Password, newPassword = "abc" }))).Should().Be("WEAK_PASSWORD");
        Code(await Json(await a.Client.PutAsJsonAsync("/api/v1/me",
            new { currentPassword = Password, newPassword = Password }))).Should().Be("PASSWORD_SAME_AS_CURRENT");
    }

    [Fact]
    public async Task Other_sessions_are_signed_out_but_this_one_stays()
    {
        var a = await SignedIn();

        // A second device signs in with the same account.
        var other = (await Json(await _anon.PostAsJsonAsync("/api/v1/auth/login", new { phone = a.Phone, password = Password })))
            .GetProperty("data").GetProperty("refreshToken").GetString()!;

        await a.Client.PutAsJsonAsync("/api/v1/me", new { currentPassword = Password, newPassword = NewPassword });

        var otherRefresh = await Json(await _anon.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = other }));
        otherRefresh.GetProperty("success").GetBoolean().Should().BeFalse();

        var thisRefresh = await Json(await _anon.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = a.Refresh }));
        thisRefresh.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Without_the_password_fields_the_password_is_untouched()
    {
        var a = await SignedIn();

        var put = await Json(await a.Client.PutAsJsonAsync("/api/v1/me", new { username = "Just Name" }));

        put.GetProperty("data").GetProperty("passwordChanged").GetBoolean().Should().BeFalse();
        (await CanLogin(a.Phone, Password)).Should().BeTrue();
    }
}
