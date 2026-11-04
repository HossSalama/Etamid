using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Config;
using Etamid.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Wallet v1: locked until activation, balance + held + ledger history,
/// withdrawal to the registered bank account with operator approval and a
/// manual bank transfer confirmed by reference.
/// </summary>
public class WalletTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const string IbanRajhi = "SA6180000000608010167542";
    // Distinct valid Rajhi IBANs: tests in one class share one database, and
    // saving the same IBAN for two users is a 409, so each bank-saving test
    // needs its own (all pass the mod-97 check, bank code 80 = Al Rajhi).
    private const string IbanRajhi2 = "SA7380000000608010167520";
    private const string IbanRajhi3 = "SA6780000000608010167531";
    private static int _counter = 9000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05099{n:D5}";
        var email = $"wallet.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Wallet User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private async Task<long> ReleasedDeal(Party buyer, Party seller)
    {
        var create = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var id = (await Json(create)).GetProperty("data").GetProperty("id").GetInt64();

        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        var providerPaymentId = (await Json(fund)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount = 1000m, currency = "SAR" });
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(webhook)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        var release = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());
        release.StatusCode.Should().Be(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        return id;
    }

    private async Task<long> UserIdOf(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync();
    }

    /// <summary>Appends a user to the wallet.operators setting (single row per key in SQL Server).</summary>
    private async Task MakeOperator(long userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "wallet.operators");
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = "wallet.operators", Value = userId.ToString(),
                ValueType = SettingValueType.String, UpdatedAt = DateTime.UtcNow
            });
        }
        else if (!row.Value.Split(',').Select(p => p.Trim()).Contains(userId.ToString()))
        {
            row.Value = row.Value + "," + userId;
        }
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static string ErrorCode(JsonElement root)
        => root.GetProperty("errors")[0].GetProperty("code").GetString()!;

    [Fact]
    public async Task Inactive_wallet_hides_balances_and_activates()
    {
        var user = await NewUser();

        var before = await Data(await user.Client.GetAsync("/api/v1/me/wallet"));
        before.GetProperty("activated").GetBoolean().Should().BeFalse();
        before.GetProperty("availableBalance").ValueKind.Should().Be(JsonValueKind.Null);
        before.GetProperty("escrowHeld").ValueKind.Should().Be(JsonValueKind.Null);

        var activate = await Post(user.Client, "/api/v1/me/wallet/activate");
        activate.StatusCode.Should().Be(HttpStatusCode.OK, await activate.Content.ReadAsStringAsync());
        var activated = await Data(activate);
        activated.GetProperty("activated").GetBoolean().Should().BeTrue();
        activated.GetProperty("availableBalance").GetDecimal().Should().Be(0m);
        activated.GetProperty("escrowHeld").GetDecimal().Should().Be(0m);

        var again = await Data(await user.Client.GetAsync("/api/v1/me/wallet"));
        again.GetProperty("activated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Release_credits_balance_and_history()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        await ReleasedDeal(buyer, seller);

        // The release creates the wallet but leaves it locked: activate first.
        (await Post(seller.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);

        var wallet = await Data(await seller.Client.GetAsync("/api/v1/me/wallet"));
        // Deal 1000 minus the default fixed platform fee of 50.
        wallet.GetProperty("availableBalance").GetDecimal().Should().Be(950m);
        wallet.GetProperty("escrowHeld").GetDecimal().Should().Be(0m);

        var tx = await Json(await seller.Client.GetAsync("/api/v1/me/wallet/transactions"));
        tx.GetProperty("meta").GetProperty("total").GetInt32().Should().Be(1);
        var entry = tx.GetProperty("data")[0];
        entry.GetProperty("type").GetString().Should().Be("escrow_release");
        entry.GetProperty("direction").GetString().Should().Be("credit");
        entry.GetProperty("amount").GetDecimal().Should().Be(950m);
        entry.GetProperty("balanceAfter").GetDecimal().Should().Be(950m);
    }

    [Fact]
    public async Task Withdrawal_full_flow_with_operator()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        await ReleasedDeal(buyer, seller);
        (await Post(seller.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);
        var save = await seller.Client.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "Wallet Seller", iban = IbanRajhi });
        save.StatusCode.Should().Be(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());

        var key = Key();
        var request = await Post(seller.Client, "/api/v1/me/wallet/withdrawals", new { amount = 400m, idempotencyKey = key });
        request.StatusCode.Should().Be(HttpStatusCode.Created, await request.Content.ReadAsStringAsync());
        var created = await Data(request);
        created.GetProperty("status").GetString().Should().Be("pending");
        created.GetProperty("ibanSnapshotMasked").GetString().Should().EndWith("7542");
        var withdrawalId = created.GetProperty("id").GetInt64();

        // Replayed key returns the same request, never a second one.
        var replay = await Post(seller.Client, "/api/v1/me/wallet/withdrawals", new { amount = 400m, idempotencyKey = key });
        (await Data(replay)).GetProperty("id").GetInt64().Should().Be(withdrawalId);

        var operatorUser = await NewUser();
        await MakeOperator(await UserIdOf(operatorUser.Phone));

        var approve = await Post(operatorUser.Client, $"/api/v1/admin/withdrawals/{withdrawalId}/approve");
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        (await Data(approve)).GetProperty("status").GetString().Should().Be("approved");

        var complete = await Post(operatorUser.Client, $"/api/v1/admin/withdrawals/{withdrawalId}/complete",
            new { bankReference = "TRF-123" });
        complete.StatusCode.Should().Be(HttpStatusCode.OK, await complete.Content.ReadAsStringAsync());
        var done = await Data(complete);
        done.GetProperty("status").GetString().Should().Be("completed");
        done.GetProperty("bankReference").GetString().Should().Be("TRF-123");

        var wallet = await Data(await seller.Client.GetAsync("/api/v1/me/wallet"));
        wallet.GetProperty("availableBalance").GetDecimal().Should().Be(550m);

        var tx = await Json(await seller.Client.GetAsync("/api/v1/me/wallet/transactions"));
        tx.GetProperty("meta").GetProperty("total").GetInt32().Should().Be(2);
        var debit = tx.GetProperty("data")[0];
        debit.GetProperty("type").GetString().Should().Be("withdrawal");
        debit.GetProperty("direction").GetString().Should().Be("debit");
        debit.GetProperty("amount").GetDecimal().Should().Be(400m);
        debit.GetProperty("balanceAfter").GetDecimal().Should().Be(550m);
    }

    [Fact]
    public async Task Withdrawal_validation()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        await ReleasedDeal(buyer, seller);
        (await Post(seller.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);
        var save = await seller.Client.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "Wallet Seller", iban = IbanRajhi2 });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var tooMuch = await Post(seller.Client, "/api/v1/me/wallet/withdrawals", new { amount = 10000m, idempotencyKey = Key() });
        tooMuch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ErrorCode(await Json(tooMuch)).Should().Be("WALLET_INSUFFICIENT_BALANCE");

        var noBank = await NewUser();
        (await Post(noBank.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);
        var noBankReq = await Post(noBank.Client, "/api/v1/me/wallet/withdrawals", new { amount = 10m, idempotencyKey = Key() });
        noBankReq.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        ErrorCode(await Json(noBankReq)).Should().Be("WALLET_BANK_ACCOUNT_REQUIRED");

        var inactive = await NewUser();
        var inactiveReq = await Post(inactive.Client, "/api/v1/me/wallet/withdrawals", new { amount = 10m, idempotencyKey = Key() });
        inactiveReq.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ErrorCode(await Json(inactiveReq)).Should().Be("WALLET_NOT_ACTIVATED");
        var inactiveTx = await inactive.Client.GetAsync("/api/v1/me/wallet/transactions");
        inactiveTx.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_requires_operator()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        await ReleasedDeal(buyer, seller);
        (await Post(seller.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);
        var save = await seller.Client.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "Wallet Seller", iban = IbanRajhi3 });
        save.StatusCode.Should().Be(HttpStatusCode.OK);
        var request = await Post(seller.Client, "/api/v1/me/wallet/withdrawals", new { amount = 100m, idempotencyKey = Key() });
        var withdrawalId = (await Data(request)).GetProperty("id").GetInt64();

        var stranger = await NewUser();
        var denied = await Post(stranger.Client, $"/api/v1/admin/withdrawals/{withdrawalId}/approve");
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ErrorCode(await Json(denied)).Should().Be("WALLET_NOT_OPERATOR");

        var operatorUser = await NewUser();
        await MakeOperator(await UserIdOf(operatorUser.Phone));
        var missing = await Post(operatorUser.Client, "/api/v1/admin/withdrawals/999999999/approve");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ErrorCode(await Json(missing)).Should().Be("WALLET_WITHDRAWAL_NOT_FOUND");
    }
}
