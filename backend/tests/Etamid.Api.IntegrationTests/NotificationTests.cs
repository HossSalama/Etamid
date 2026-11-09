using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Config;
using Etamid.Domain.Money;
using Etamid.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Notifications Layer 1 (in-app only): events create a row for the right
/// recipient in the same save, and the list/read/unread-count endpoints are
/// scoped to the caller's own notifications.
/// </summary>
public class NotificationTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const long DisputeReason = 7;
    private const string IbanRajhi = "SA3480000000608010167543"; // Rajhi IBAN, mod-97 valid, distinct from the three WalletTests uses
    private static int _counter = 800;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static string ErrorCode(JsonElement root)
        => root.GetProperty("errors")[0].GetProperty("code").GetString()!;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05098{n:D5}";
        var email = $"notif.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Notif User " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (key is not null) request.Headers.Add("X-Idempotency-Key", key);
        return await c.SendAsync(request);
    }

    private static string Key() => Guid.NewGuid().ToString("N");

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

    private async Task<string> StartFunding(Party buyer, long dealId, string? key = null)
    {
        var r = await Post(buyer.Client, $"/api/v1/deals/{dealId}/fund", null, key ?? Key());
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
    }

    private async Task SendWebhook(string eventId, string type, string providerPaymentId, decimal amount)
    {
        var body = JsonSerializer.Serialize(new { eventId, type, providerPaymentId, amount, currency = "SAR" });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<long> FundedDeal(Party buyer, Party seller, decimal amount = 1000m, int hours = 24)
    {
        var id = await CreateDeal(buyer, seller, amount, hours);
        await Accept(seller, id);
        var providerPaymentId = await StartFunding(buyer, id);
        await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, amount);
        return id;
    }

    private async Task<long> InspectionDeal(Party buyer, Party seller, decimal amount = 1000m, int hours = 24)
    {
        var id = await FundedDeal(buyer, seller, amount, hours);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<long> ReleasedDeal(Party buyer, Party seller)
    {
        var id = await InspectionDeal(buyer, seller);
        var release = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());
        release.StatusCode.Should().Be(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        return id;
    }

    private static async Task<JsonElement> List(HttpClient c)
        => await Data(await c.GetAsync("/api/v1/notifications?page=1&pageSize=20"));

    private static async Task<int> UnreadCount(HttpClient c)
        => (await Data(await c.GetAsync("/api/v1/notifications/unread-count"))).GetProperty("count").GetInt32();

    private async Task<long> UserIdOf(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync();
    }

    /// <summary>Appends a user to the disputes.operators setting (single row per key in SQL Server).</summary>
    private async Task MakeDisputeOperator(long userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "disputes.operators");
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = "disputes.operators", Value = userId.ToString(),
                ValueType = SettingValueType.String, UpdatedAt = DateTime.UtcNow
            });
        }
        else if (!row.Value.Split(',').Select(p => p.Trim()).Contains(userId.ToString()))
        {
            row.Value = row.Value + "," + userId;
        }
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ tests
    [Fact]
    public async Task Deal_created_notifies_seller_only()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var dealId = await CreateDeal(buyer, seller);

        var sellerItems = await List(seller.Client);
        sellerItems.GetArrayLength().Should().Be(1);
        var item = sellerItems[0];
        item.GetProperty("type").GetString().Should().Be("deal_request");
        item.GetProperty("dealId").GetInt64().Should().Be(dealId);
        item.GetProperty("isRead").GetBoolean().Should().BeFalse();
        item.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("body").GetString().Should().NotBeNullOrEmpty();

        (await List(buyer.Client)).GetArrayLength().Should().Be(0);
        (await UnreadCount(seller.Client)).Should().Be(1);
        (await UnreadCount(buyer.Client)).Should().Be(0);
    }

    [Fact]
    public async Task Accept_notifies_buyer_and_funding_notifies_seller_newest_first()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var dealId = await FundedDeal(buyer, seller);

        var buyerItems = await List(buyer.Client);
        buyerItems.GetArrayLength().Should().Be(1);
        buyerItems[0].GetProperty("type").GetString().Should().Be("deal_accepted");
        buyerItems[0].GetProperty("dealId").GetInt64().Should().Be(dealId);

        var sellerItems = await List(seller.Client);
        sellerItems.GetArrayLength().Should().Be(2);
        sellerItems[0].GetProperty("type").GetString().Should().Be("deal_funded");
        sellerItems[1].GetProperty("type").GetString().Should().Be("deal_request");
        (await UnreadCount(seller.Client)).Should().Be(2);
    }

    [Fact]
    public async Task Mark_read_is_idempotent_and_scoped_to_owner()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        await FundedDeal(buyer, seller);

        var sellerItems = await List(seller.Client);
        var fundedId = sellerItems[0].GetProperty("id").GetInt64();

        var read = await Post(seller.Client, $"/api/v1/notifications/{fundedId}/read");
        read.StatusCode.Should().Be(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
        (await UnreadCount(seller.Client)).Should().Be(1);

        // Reading again is an idempotent success, not an error.
        var reread = await Post(seller.Client, $"/api/v1/notifications/{fundedId}/read");
        reread.StatusCode.Should().Be(HttpStatusCode.OK);
        (await UnreadCount(seller.Client)).Should().Be(1);

        // Another user's notification id is NOT_FOUND: ids cannot be probed.
        var stranger = await Post(buyer.Client, $"/api/v1/notifications/{fundedId}/read");
        stranger.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(stranger)).Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Read_all_marks_only_caller_notifications()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        await FundedDeal(buyer, seller);

        var readAll = await Post(seller.Client, "/api/v1/notifications/read-all");
        readAll.StatusCode.Should().Be(HttpStatusCode.OK, await readAll.Content.ReadAsStringAsync());
        (await Data(readAll)).GetProperty("markedCount").GetInt32().Should().Be(2);
        (await UnreadCount(seller.Client)).Should().Be(0);
        (await List(seller.Client))[0].GetProperty("isRead").GetBoolean().Should().BeTrue();

        // The buyer's own notification is untouched.
        (await UnreadCount(buyer.Client)).Should().Be(1);
    }

    [Fact]
    public async Task Dispute_opened_notifies_other_party()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);

        var open = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "Item arrived damaged" });
        open.StatusCode.Should().Be(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());
        var disputeId = (await Json(open)).GetProperty("data").GetProperty("id").GetInt64();

        var buyerItems = await List(buyer.Client);
        var update = buyerItems.EnumerateArray().FirstOrDefault(i => i.GetProperty("type").GetString() == "dispute_update");
        update.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        update.GetProperty("dealId").GetInt64().Should().Be(dealId);
        update.GetProperty("disputeId").GetInt64().Should().Be(disputeId);
    }

    [Fact]
    public async Task Release_notifies_seller_wallet()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await ReleasedDeal(buyer, seller);

        var sellerItems = await List(seller.Client);
        var wallet = sellerItems.EnumerateArray().FirstOrDefault(i => i.GetProperty("type").GetString() == "wallet");
        wallet.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        wallet.GetProperty("dealId").GetInt64().Should().Be(dealId);
    }

    [Fact]
    public async Task Withdrawal_requested_notifies_owner()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        await ReleasedDeal(buyer, seller);

        var activate = await Post(seller.Client, "/api/v1/me/wallet/activate", new { });
        activate.StatusCode.Should().Be(HttpStatusCode.OK, await activate.Content.ReadAsStringAsync());
        var save = await seller.Client.PutAsJsonAsync("/api/v1/me/bank-account",
            new { holderName = "Notif Seller", iban = IbanRajhi });
        save.StatusCode.Should().Be(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());

        var before = await UnreadCount(seller.Client);
        var request = await Post(seller.Client, "/api/v1/me/wallet/withdrawals",
            new { amount = 100m, idempotencyKey = Key() });
        request.StatusCode.Should().Be(HttpStatusCode.Created, await request.Content.ReadAsStringAsync());

        (await UnreadCount(seller.Client)).Should().Be(before + 1);
        var wallet = (await List(seller.Client)).EnumerateArray()
            .First(i => i.GetProperty("type").GetString() == "wallet");
        wallet.GetProperty("isRead").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Internal_dispute_note_notifies_nobody()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var operatorUser = await NewUser();
        await MakeDisputeOperator(await UserIdOf(operatorUser.Phone));
        var dealId = await InspectionDeal(buyer, seller);

        var open = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "Wrong item received" });
        open.StatusCode.Should().Be(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());

        var buyerBefore = await UnreadCount(buyer.Client);
        var sellerBefore = await UnreadCount(seller.Client);

        var note = await Post(operatorUser.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "Internal: check the photos", isInternalNote = true });
        note.StatusCode.Should().Be(HttpStatusCode.Created, await note.Content.ReadAsStringAsync());

        (await UnreadCount(buyer.Client)).Should().Be(buyerBefore);
        (await UnreadCount(seller.Client)).Should().Be(sellerBefore);
    }
}
