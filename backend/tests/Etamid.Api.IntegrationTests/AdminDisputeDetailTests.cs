using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Api.Extensions;
using Etamid.Application.Common.Interfaces;
using Etamid.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Admin dispute detail: GET /api/v1/admin/disputes/{id} returns the full detail
/// (reason name, details, opener name, messages) that the list endpoint never
/// carried, and every timestamp is emitted as UTC with 'Z'.
/// Errors are asserted from the response BODY (error code), never from the HTTP
/// status, because main answers API errors with HTTP 200 and the code in the body.
/// </summary>
public class AdminDisputeDetailTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const long DisputeReason = 7; // "لم أستلم المنتج" / "I did not receive the product"
    private static int _counter = 31000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone, string Name, string Email);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static async Task<bool> Succeeded(HttpResponseMessage r)
        => (await Json(r)).TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;

    private static async Task<string?> ErrorCode(HttpResponseMessage r)
    {
        var root = await Json(r);
        return root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
            ? errors[0].GetProperty("code").GetString()
            : null;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (key is not null) request.Headers.Add("X-Idempotency-Key", key);
        return await c.SendAsync(request);
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var phone = $"05093{n:D5}";
        var email = $"dispute-detail.{n}@example.com";
        var name = "Dispute Detail User " + n;

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = name, phone, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = factory.Emails.CodeFor(email) });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, phone, name, email);
    }

    private static async Task<long> CreateDeal(Party buyer, Party seller, decimal amount = 1000m, int hours = 24)
    {
        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount, durationValue = hours, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private static async Task Accept(Party seller, long dealId)
        => (await Post(seller.Client, $"/api/v1/deals/{dealId}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);

    private static async Task<string> StartFunding(Party buyer, long dealId, string? key = null)
    {
        var r = await Post(buyer.Client, $"/api/v1/deals/{dealId}/fund", null, key ?? Key());
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
    }

    private async Task<HttpResponseMessage> SendWebhook(string eventId, string type, string providerPaymentId, decimal amount)
    {
        var body = JsonSerializer.Serialize(new { eventId, type, providerPaymentId, amount, currency = "SAR" });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        return await _anon.SendAsync(request);
    }

    private async Task<long> InspectionDeal(Party buyer, Party seller)
    {
        var id = await CreateDeal(buyer, seller);
        await Accept(seller, id);
        var providerPaymentId = await StartFunding(buyer, id);
        (await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, 1000m)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private static async Task<long> OpenDispute(Party opener, long dealId)
    {
        var r = await Post(opener.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "The item arrived damaged." });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private Task<long> UserIdOf(string phone) => Db(db =>
        db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync());

    private async Task<(long DisputeId, Party Buyer, Party Seller)> DisputeWithOneMessage()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        var disputeId = await OpenDispute(buyer, dealId);

        var message = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "The screen is cracked." });
        message.StatusCode.Should().Be(HttpStatusCode.Created, await message.Content.ReadAsStringAsync());
        return (disputeId, buyer, seller);
    }

    // ------------------------------------------------------------------ detail endpoint

    [Fact]
    public async Task Detail_returns_reason_details_opener_and_messages()
    {
        var (disputeId, buyer, _) = await DisputeWithOneMessage();
        var admin = await AdminTestKit.CreateAsync(factory, "admin.disputes.view");

        var r = await admin.Client.GetAsync($"/api/v1/admin/disputes/{disputeId}");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        (await Succeeded(r)).Should().BeTrue(await r.Content.ReadAsStringAsync());
        var detail = await Data(r);

        detail.GetProperty("id").GetInt64().Should().Be(disputeId);
        detail.GetProperty("dealId").GetInt64().Should().BeGreaterThan(0);
        detail.GetProperty("status").GetString().Should().Be("under_review");
        detail.GetProperty("role").GetString().Should().Be("operator");
        detail.GetProperty("reasonId").GetInt64().Should().Be(DisputeReason);
        detail.GetProperty("reasonNameAr").GetString().Should().Be("لم أستلم المنتج");
        detail.GetProperty("reasonNameEn").GetString().Should().Be("I did not receive the product");
        detail.GetProperty("details").GetString().Should().Be("The item arrived damaged.");
        detail.GetProperty("openedById").GetInt64().Should().Be(await UserIdOf(buyer.Phone));
        detail.GetProperty("openedByName").GetString().Should().Be(buyer.Name);

        var messages = detail.GetProperty("messages");
        messages.GetArrayLength().Should().Be(1);
        messages[0].GetProperty("senderType").GetString().Should().Be("buyer");
        messages[0].GetProperty("senderName").GetString().Should().Be(buyer.Name);
        messages[0].GetProperty("message").GetString().Should().Be("The screen is cracked.");
    }

    [Fact]
    public async Task Detail_timestamps_are_utc_with_Z()
    {
        var (disputeId, _, _) = await DisputeWithOneMessage();
        var admin = await AdminTestKit.CreateAsync(factory, "admin.disputes.view");

        var detail = await Data(await admin.Client.GetAsync($"/api/v1/admin/disputes/{disputeId}"));

        detail.GetProperty("createdAt").GetString().Should().EndWith("Z");
        detail.GetProperty("messages")[0].GetProperty("createdAt").GetString().Should().EndWith("Z");
    }

    [Fact]
    public async Task Detail_unknown_id_returns_DISPUTE_NOT_FOUND()
    {
        var admin = await AdminTestKit.CreateAsync(factory, "admin.disputes.view");

        var r = await admin.Client.GetAsync("/api/v1/admin/disputes/999999999");
        (await Succeeded(r)).Should().BeFalse(await r.Content.ReadAsStringAsync());
        (await ErrorCode(r)).Should().Be("DISPUTE_NOT_FOUND");
    }

    [Fact]
    public async Task Detail_without_permission_returns_ADMIN_FORBIDDEN()
    {
        var (disputeId, _, _) = await DisputeWithOneMessage();
        var admin = await AdminTestKit.CreateAsync(factory); // no permissions

        var r = await admin.Client.GetAsync($"/api/v1/admin/disputes/{disputeId}");
        (await Succeeded(r)).Should().BeFalse(await r.Content.ReadAsStringAsync());
        (await ErrorCode(r)).Should().Be("ADMIN_FORBIDDEN");
    }

    // ------------------------------------------------------------------ UTC converter

    private static JsonSerializerOptions ConverterOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.Converters.Add(new UtcNullableDateTimeJsonConverter());
        return options;
    }

    [Fact]
    public void Converter_writes_Z_for_unspecified_and_local()
    {
        var unspecified = new DateTime(2026, 11, 15, 9, 14, 0, DateTimeKind.Unspecified);
        JsonSerializer.Serialize(unspecified, ConverterOptions()).Should().Be("\"2026-11-15T09:14:00Z\"");

        var local = new DateTime(2026, 11, 15, 11, 14, 0, DateTimeKind.Local);
        var written = JsonSerializer.Serialize(local, ConverterOptions());
        written.Should().EndWith("Z\"");
        var parsed = DateTime.Parse(written.Trim('"'), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        parsed.Should().Be(DateTime.SpecifyKind(local.ToUniversalTime(), DateTimeKind.Utc));
    }

    [Fact]
    public void Converter_reads_no_offset_as_utc_and_offset_as_utc()
    {
        var plain = JsonSerializer.Deserialize<DateTime>("\"2026-11-15T09:14:00\"", ConverterOptions());
        plain.Kind.Should().Be(DateTimeKind.Utc);
        plain.Should().Be(new DateTime(2026, 11, 15, 9, 14, 0, DateTimeKind.Utc));

        var offset = JsonSerializer.Deserialize<DateTime>("\"2026-11-15T11:14:00+02:00\"", ConverterOptions());
        offset.Kind.Should().Be(DateTimeKind.Utc);
        offset.Should().Be(new DateTime(2026, 11, 15, 9, 14, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Converter_nullable_round_trips_null()
    {
        JsonSerializer.Deserialize<DateTime?>("null", ConverterOptions()).Should().BeNull();
        JsonSerializer.Serialize<DateTime?>(null, ConverterOptions()).Should().Be("null");
    }
}
