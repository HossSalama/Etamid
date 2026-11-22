using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>PUT /me accepts the login phone (the "edit profile" screen shows it next to username and email).</summary>
public class ProfilePhoneTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
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
        var phone = $"05033{n:D5}";
        var email = $"pp.{n}@example.com";

        await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "User " + n, phone, email, password = Password, acceptTerms = true });
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = factory.Emails.CodeFor(email) });
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Account(client, phone, email);
    }

    [Fact]
    public async Task Changing_the_phone_moves_the_login_to_the_new_number()
    {
        var a = await SignedIn();
        var newPhone = $"05044{Interlocked.Increment(ref _counter):D5}";

        var put = await a.Client.PutAsJsonAsync("/api/v1/me", new { username = "Nada Alsharawi", phone = newPhone });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await Json(put)).GetProperty("data");
        data.GetProperty("phoneChanged").GetBoolean().Should().BeTrue();
        data.GetProperty("emailChanged").GetBoolean().Should().BeFalse();

        var me = (await Json(await a.Client.GetAsync("/api/v1/me"))).GetProperty("data");
        me.GetProperty("phone").GetString().Should().Be(newPhone);
        me.GetProperty("name").GetString().Should().Be("Nada Alsharawi");
        // A new number never inherits the old one's verification.
        me.GetProperty("phoneVerified").GetBoolean().Should().BeFalse();

        var oldLogin = await _anon.PostAsJsonAsync("/api/v1/auth/login", new { phone = a.Phone, password = Password });
        Code(await Json(oldLogin)).Should().Be("INVALID_CREDENTIALS");

        var newLogin = await _anon.PostAsJsonAsync("/api/v1/auth/login", new { phone = newPhone, password = Password });
        (await Json(newLogin)).GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Sending_the_same_phone_changes_nothing()
    {
        var a = await SignedIn();

        var put = await a.Client.PutAsJsonAsync("/api/v1/me", new { phone = a.Phone });

        (await Json(put)).GetProperty("data").GetProperty("phoneChanged").GetBoolean().Should().BeFalse();
        var me = (await Json(await a.Client.GetAsync("/api/v1/me"))).GetProperty("data");
        me.GetProperty("phone").GetString().Should().Be(a.Phone);
    }

    [Fact]
    public async Task A_phone_without_the_field_keeps_the_old_behaviour()
    {
        var a = await SignedIn();

        var put = await a.Client.PutAsJsonAsync("/api/v1/me", new { username = "Only Name" });

        (await Json(put)).GetProperty("data").GetProperty("phoneChanged").GetBoolean().Should().BeFalse();
        var me = (await Json(await a.Client.GetAsync("/api/v1/me"))).GetProperty("data");
        me.GetProperty("phone").GetString().Should().Be(a.Phone);
    }

    [Fact]
    public async Task A_phone_owned_by_another_account_is_refused()
    {
        var a = await SignedIn();
        var b = await SignedIn();

        var put = await a.Client.PutAsJsonAsync("/api/v1/me", new { phone = b.Phone });

        put.StatusCode.Should().Be(HttpStatusCode.OK);
        Code(await Json(put)).Should().Be("PHONE_EXISTS");

        // Nothing leaked into the failed edit.
        var me = (await Json(await a.Client.GetAsync("/api/v1/me"))).GetProperty("data");
        me.GetProperty("phone").GetString().Should().Be(a.Phone);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("0401234567")]
    [InlineData("+971501234567")]
    [InlineData("abc")]
    public async Task A_non_saudi_or_malformed_phone_is_refused(string bad)
    {
        var a = await SignedIn();

        var put = await a.Client.PutAsJsonAsync("/api/v1/me", new { phone = bad });

        Code(await Json(put)).Should().Be("INVALID_PHONE");
    }

    [Fact]
    public async Task Changing_phone_and_email_together_sends_the_email_code_for_the_new_phone()
    {
        var a = await SignedIn();
        var newPhone = $"05055{Interlocked.Increment(ref _counter):D5}";
        var newEmail = $"pp.new.{_counter}@example.com";

        var put = await a.Client.PutAsJsonAsync("/api/v1/me", new { email = newEmail, phone = newPhone });
        var data = (await Json(put)).GetProperty("data");
        data.GetProperty("phoneChanged").GetBoolean().Should().BeTrue();
        data.GetProperty("emailChanged").GetBoolean().Should().BeTrue();

        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new { phone = newPhone, code = factory.Emails.CodeFor(newEmail) });
        (await Json(verify)).GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task The_phone_change_is_written_to_the_audit_log()
    {
        var a = await SignedIn();
        var newPhone = $"05066{Interlocked.Increment(ref _counter):D5}";

        await a.Client.PutAsJsonAsync("/api/v1/me", new { phone = newPhone });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Etamid.Application.Common.Interfaces.IApplicationDbContext>();
        var normalized = "+966" + newPhone[1..];
        db.AuthAuditLogs.Any(l => l.Event == Etamid.Domain.Identity.AuthEvent.PhoneChanged && l.PhoneNormalized == normalized)
            .Should().BeTrue();
    }
}
