using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>DELETE /me: the "حذف الحساب" button on the edit-profile screen.</summary>
public class DeleteAccountTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 100;

    private readonly HttpClient _anon = factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static string Code(JsonElement body) => body.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private sealed record Account(HttpClient Client, string Phone, string Email);

    private async Task<Account> SignedIn()
    {
        var n = Interlocked.Increment(ref _counter);
        var phone = $"05077{n:D5}";
        var email = $"del.{n}@example.com";

        await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "User " + n, phone, email, password = Password, acceptTerms = true });
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = factory.Emails.CodeFor(email) });
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Account(client, phone, email);
    }

    private static Task<HttpResponseMessage> Delete(HttpClient c, object body)
        => c.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me") { Content = JsonContent.Create(body) });

    [Fact]
    public async Task A_correct_password_deletes_the_account_and_ends_the_login()
    {
        var a = await SignedIn();

        var del = await Delete(a.Client, new { password = Password });

        del.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(del)).GetProperty("success").GetBoolean().Should().BeTrue();

        var login = await _anon.PostAsJsonAsync("/api/v1/auth/login", new { phone = a.Phone, password = Password });
        Code(await Json(login)).Should().Be("INVALID_CREDENTIALS");

        var me = await Json(await a.Client.GetAsync("/api/v1/me"));
        me.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_wrong_password_keeps_the_account()
    {
        var a = await SignedIn();

        var del = await Delete(a.Client, new { password = "Wrong#999" });

        Code(await Json(del)).Should().Be("WRONG_PASSWORD");
        var login = await _anon.PostAsJsonAsync("/api/v1/auth/login", new { phone = a.Phone, password = Password });
        (await Json(login)).GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_missing_password_is_refused()
    {
        var a = await SignedIn();

        var del = await Delete(a.Client, new { });

        Code(await Json(del)).Should().Be("REQUIRED");
    }

    [Fact]
    public async Task The_phone_can_register_again_after_deletion()
    {
        var a = await SignedIn();
        await Delete(a.Client, new { password = Password });

        var reg = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Again", phone = a.Phone, email = a.Email, password = Password, acceptTerms = true });

        (await Json(reg)).GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task An_open_deal_blocks_the_deletion()
    {
        var seller = await SignedIn();
        var buyer = await SignedIn();

        var create = await seller.Client.PostAsJsonAsync("/api/v1/deals", new
        {
            title = "Phone", buyerPhoneOrCode = buyer.Phone, description = "Used phone", amount = 500m,
            durationValue = 3, durationUnit = "days", needShipping = false, acceptTerms = true
        });
        (await Json(create)).GetProperty("success").GetBoolean().Should().BeTrue();

        var del = await Delete(seller.Client, new { password = Password });

        Code(await Json(del)).Should().Be("ACCOUNT_HAS_ACTIVE_DEALS");
        var me = await Json(await seller.Client.GetAsync("/api/v1/me"));
        me.GetProperty("success").GetBoolean().Should().BeTrue();
    }
}
