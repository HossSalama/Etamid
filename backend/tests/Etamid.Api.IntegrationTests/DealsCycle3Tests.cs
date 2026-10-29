using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Deals.Commands;
using Etamid.Domain.Money;
using Etamid.Infrastructure.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>Cycle 3 quality gate: deals, digital escrow ledger, webhook safety, idempotency, permissions, disputes.</summary>
public class DealsCycle3Tests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const long RejectReason = 1;
    private const long DisputeReason = 7;
    private static int _counter = 300;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<string> ErrorCode(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05033{n:D5}";
        var email = $"c3.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Deal User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private static async Task<string> StatusOf(Party who, long dealId)
        => (await Json(await who.Client.GetAsync($"/api/v1/deals/{dealId}"))).GetProperty("data").GetProperty("status").GetString()!;

    private static async Task Accept(Party seller, long dealId)
        => (await Post(seller.Client, $"/api/v1/deals/{dealId}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);

    private static async Task<string> StartFunding(Party buyer, long dealId, string? key = null)
    {
        var r = await Post(buyer.Client, $"/api/v1/deals/{dealId}/fund", null, key ?? Key());
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("providerPaymentId").GetString()!;
    }

    private async Task<HttpResponseMessage> SendWebhook(string eventId, string type, string providerPaymentId, decimal amount, string? signatureOverride = null)
    {
        var body = JsonSerializer.Serialize(new { eventId, type, providerPaymentId, amount, currency = "SAR" });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Signature", signatureOverride ?? HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        return await _anon.SendAsync(request);
    }

    private async Task<long> FundedDeal(Party buyer, Party seller, decimal amount = 1000m, int hours = 24)
    {
        var id = await CreateDeal(buyer, seller, amount, hours);
        await Accept(seller, id);
        var providerPaymentId = await StartFunding(buyer, id);
        (await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, amount)).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<long> InspectionDeal(Party buyer, Party seller, decimal amount = 1000m, int hours = 24)
    {
        var id = await FundedDeal(buyer, seller, amount, hours);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        return id;
    }

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private record LedgerView(List<LedgerTransaction> Transactions, List<LedgerEntry> Entries);

    private Task<LedgerView> LedgerOf(long dealId) => Db(async db =>
    {
        var txs = await db.LedgerTransactions.AsNoTracking().Where(t => t.DealId == dealId).ToListAsync();
        var ids = txs.Select(t => t.Id).ToList();
        var entries = await db.LedgerEntries.AsNoTracking().Where(e => ids.Contains(e.TransactionId)).ToListAsync();
        return new LedgerView(txs, entries);
    });

    private static void AssertBalanced(LedgerView ledger)
    {
        foreach (var tx in ledger.Transactions)
        {
            var lines = ledger.Entries.Where(e => e.TransactionId == tx.Id).ToList();
            lines.Where(e => e.Direction == LedgerDirection.Debit).Sum(e => e.Amount)
                .Should().Be(lines.Where(e => e.Direction == LedgerDirection.Credit).Sum(e => e.Amount), $"transaction {tx.Type} must balance");
        }
    }

    private Task<decimal> NetCredit(LedgerAccountType type) => Db(async db =>
    {
        var accountIds = await db.LedgerAccounts.AsNoTracking().Where(a => a.OwnerUserId == null && a.Type == type).Select(a => a.Id).ToListAsync();
        var entries = await db.LedgerEntries.AsNoTracking().Where(e => accountIds.Contains(e.AccountId)).ToListAsync();
        return entries.Sum(e => e.Direction == LedgerDirection.Credit ? e.Amount : -e.Amount);
    });

    private Task<decimal> SellerWallet(long dealId) => Db(async db =>
    {
        var sellerId = await db.Deals.AsNoTracking().Where(d => d.Id == dealId).Select(d => d.SellerId).FirstAsync();
        var wallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == sellerId);
        return wallet?.AvailableBalance ?? 0m;
    });

    // ------------------------------------------------------------------ 1. happy path
    [Fact]
    public async Task Happy_path_from_creation_to_release_writes_balanced_ledger()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var id = await CreateDeal(buyer, seller);
        (await StatusOf(buyer, id)).Should().Be("pending_acceptance");

        await Accept(seller, id);
        var accepted = (await Json(await buyer.Client.GetAsync($"/api/v1/deals/{id}"))).GetProperty("data");
        accepted.GetProperty("status").GetString().Should().Be("awaiting_funding");
        accepted.GetProperty("feeAmount").GetDecimal().Should().Be(50m);
        accepted.GetProperty("sellerNetAmount").GetDecimal().Should().Be(950m);
        accepted.GetProperty("contractHash").GetString().Should().NotBeNullOrEmpty();

        var providerPaymentId = await StartFunding(buyer, id);
        (await StatusOf(buyer, id)).Should().Be("awaiting_funding", "only the signed webhook funds a deal");

        var hook = await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, 1000m);
        hook.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(hook)).GetProperty("data").GetProperty("outcome").GetString().Should().Be("processed");
        (await StatusOf(buyer, id)).Should().Be("funded");

        var funding = await LedgerOf(id);
        funding.Transactions.Should().ContainSingle(t => t.Type == LedgerTransactionType.EscrowFunding);
        AssertBalanced(funding);
        (await NetCredit(LedgerAccountType.PlatformEscrow)).Should().BeGreaterThanOrEqualTo(1000m);

        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOf(buyer, id)).Should().Be("inspection");

        var release = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());
        release.StatusCode.Should().Be(HttpStatusCode.OK, await release.Content.ReadAsStringAsync());
        var released = (await Json(release)).GetProperty("data");
        released.GetProperty("status").GetString().Should().Be("released");
        released.GetProperty("sellerNetAmount").GetDecimal().Should().Be(950m);

        var ledger = await LedgerOf(id);
        ledger.Transactions.Should().HaveCount(2);
        AssertBalanced(ledger);
        var releaseTx = ledger.Transactions.Single(t => t.Type == LedgerTransactionType.EscrowRelease);
        var lines = ledger.Entries.Where(e => e.TransactionId == releaseTx.Id).ToList();
        lines.Single(e => e.Direction == LedgerDirection.Debit).Amount.Should().Be(1000m);
        lines.Where(e => e.Direction == LedgerDirection.Credit).Select(e => e.Amount).Should().BeEquivalentTo(new[] { 950m, 50m });

        (await SellerWallet(id)).Should().Be(950m);
        (await StatusOf(seller, id)).Should().Be("released");
    }

    // ------------------------------------------------------------------ 2. idempotency
    [Fact]
    public async Task Fund_with_same_key_twice_creates_one_payment()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        await Accept(seller, id);

        var key = Key();
        var first = await StartFunding(buyer, id, key);
        var second = await StartFunding(buyer, id, key);
        var thirdNewKey = await StartFunding(buyer, id, Key());

        second.Should().Be(first);
        thirdNewKey.Should().Be(first, "a retry with a new key must reuse the open checkout, never charge twice");
        (await Db(db => db.Payments.AsNoTracking().CountAsync(p => p.DealId == id))).Should().Be(1);
    }

    [Fact]
    public async Task Fund_requires_an_idempotency_key_and_rejects_reuse_for_another_deal()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var a = await CreateDeal(buyer, seller);
        var b = await CreateDeal(buyer, seller);
        await Accept(seller, a);
        await Accept(seller, b);

        var missing = await Post(buyer.Client, $"/api/v1/deals/{a}/fund");
        missing.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCode(missing)).Should().Be("IDEMPOTENCY_KEY_REQUIRED");

        var key = Key();
        await StartFunding(buyer, a, key);
        var reused = await Post(buyer.Client, $"/api/v1/deals/{b}/fund", null, key);
        reused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCode(reused)).Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task Webhook_delivered_twice_writes_exactly_one_ledger_transaction()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        await Accept(seller, id);
        var providerPaymentId = await StartFunding(buyer, id);

        var eventId = "evt_" + Key();
        var first = await SendWebhook(eventId, "payment.succeeded", providerPaymentId, 1000m);
        var replay = await SendWebhook(eventId, "payment.succeeded", providerPaymentId, 1000m);
        var otherEventSamePayment = await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, 1000m);

        (await Json(first)).GetProperty("data").GetProperty("outcome").GetString().Should().Be("processed");
        (await Json(replay)).GetProperty("data").GetProperty("outcome").GetString().Should().Be("duplicate");
        (await Json(otherEventSamePayment)).GetProperty("data").GetProperty("outcome").GetString().Should().Be("ignored");

        var ledger = await LedgerOf(id);
        ledger.Transactions.Should().ContainSingle(t => t.Type == LedgerTransactionType.EscrowFunding);
        (await Db(db => db.EscrowHolds.AsNoTracking().CountAsync(h => h.DealId == id))).Should().Be(1);
        (await StatusOf(buyer, id)).Should().Be("funded");
    }

    [Fact]
    public async Task Failure_webhook_arriving_after_success_never_undoes_the_payment()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        await Accept(seller, id);
        var providerPaymentId = await StartFunding(buyer, id);

        await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, 1000m);
        var late = await SendWebhook("evt_" + Key(), "payment.failed", providerPaymentId, 1000m);

        (await Json(late)).GetProperty("data").GetProperty("outcome").GetString().Should().Be("ignored");
        (await StatusOf(buyer, id)).Should().Be("funded");
        (await LedgerOf(id)).Transactions.Should().ContainSingle();
    }

    [Fact]
    public async Task Release_with_same_key_twice_releases_once()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller);

        var key = Key();
        var first = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, key);
        var replay = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, key);
        var newKey = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        newKey.StatusCode.Should().Be(HttpStatusCode.Conflict, "a different key cannot release an already released deal");

        (await LedgerOf(id)).Transactions.Count(t => t.Type == LedgerTransactionType.EscrowRelease).Should().Be(1);
        (await SellerWallet(id)).Should().Be(950m);
    }

    // ------------------------------------------------------------------ webhook trust
    [Fact]
    public async Task Webhook_with_bad_or_missing_signature_is_rejected_and_changes_nothing()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        await Accept(seller, id);
        var providerPaymentId = await StartFunding(buyer, id);

        var forged = await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, 1000m, signatureOverride: new string('a', 64));
        forged.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var unsigned = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent("{\"eventId\":\"x\",\"type\":\"payment.succeeded\",\"providerPaymentId\":\"" + providerPaymentId + "\"}", Encoding.UTF8, "application/json")
        };
        (await _anon.SendAsync(unsigned)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await StatusOf(buyer, id)).Should().Be("awaiting_funding");
        (await LedgerOf(id)).Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Webhook_with_wrong_amount_does_not_fund_the_deal()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        await Accept(seller, id);
        var providerPaymentId = await StartFunding(buyer, id);

        var hook = await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, 999m);
        (await Json(hook)).GetProperty("data").GetProperty("outcome").GetString().Should().Be("amount_mismatch");
        (await StatusOf(buyer, id)).Should().Be("awaiting_funding");
        (await LedgerOf(id)).Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Webhook_for_unknown_payment_is_not_processed()
    {
        var hook = await SendWebhook("evt_" + Key(), "payment.succeeded", "fake_doesnotexist", 1000m);
        hook.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCode(hook)).Should().Be("PAYMENT_NOT_FOUND");
    }

    // ------------------------------------------------------------------ 3. permissions
    [Fact]
    public async Task Parties_cannot_act_on_each_others_steps()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var stranger = await NewUser();
        var id = await CreateDeal(buyer, seller);

        var buyerAccepts = await Post(buyer.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true });
        buyerAccepts.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await Accept(seller, id);

        (await Post(seller.Client, $"/api/v1/deals/{id}/fund", null, Key())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var providerPaymentId = await StartFunding(buyer, id);
        await SendWebhook("evt_" + Key(), "payment.succeeded", providerPaymentId, 1000m);

        (await Post(buyer.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);

        var sellerReleases = await Post(seller.Client, $"/api/v1/deals/{id}/release", null, Key());
        sellerReleases.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the seller can never confirm receipt for the buyer");
        (await ErrorCode(sellerReleases)).Should().Be("DEAL_ACTION_NOT_ALLOWED");

        (await Post(seller.Client, $"/api/v1/deals/{id}/dispute", new { reasonId = DisputeReason, details = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await StatusOf(buyer, id)).Should().Be("inspection");
        (await LedgerOf(id)).Transactions.Should().ContainSingle(t => t.Type == LedgerTransactionType.EscrowFunding);

        // A stranger gets 404 everywhere, so deal ids cannot be probed.
        (await stranger.Client.GetAsync($"/api/v1/deals/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Post(stranger.Client, $"/api/v1/deals/{id}/release", null, Key())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Post(stranger.Client, $"/api/v1/deals/{id}/dispute", new { reasonId = DisputeReason, details = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deals_endpoints_require_authentication()
    {
        (await _anon.GetAsync("/api/v1/deals")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Post(_anon, "/api/v1/deals/1/release", null, Key())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Release_before_delivery_is_refused()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await FundedDeal(buyer, seller);

        var release = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());
        release.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LedgerOf(id)).Transactions.Should().ContainSingle(t => t.Type == LedgerTransactionType.EscrowFunding);
    }

    [Fact]
    public async Task Deliver_is_refused_before_funding()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        await Accept(seller, id);

        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------------ reject / cancel paths
    [Fact]
    public async Task Rejected_deal_cannot_be_funded()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);

        var reject = await Post(seller.Client, $"/api/v1/deals/{id}/reject", new { reasonId = RejectReason, note = "not available" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());
        (await StatusOf(buyer, id)).Should().Be("rejected");

        (await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key())).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_validates_input()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var self = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "t", sellerPhoneOrCode = buyer.Phone, description = "d", amount = 100m,
            durationValue = 1, durationUnit = "day", needShipping = false, acceptTerms = true
        });
        self.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCode(self)).Should().Be("DEAL_SAME_PARTIES");

        var badAmount = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "t", sellerPhoneOrCode = seller.Phone, description = "d", amount = 10.555m,
            durationValue = 1, durationUnit = "day", needShipping = false, acceptTerms = true
        });
        badAmount.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var noTerms = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "t", sellerPhoneOrCode = seller.Phone, description = "d", amount = 100m,
            durationValue = 1, durationUnit = "day", needShipping = false, acceptTerms = false
        });
        noTerms.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var unknownSeller = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "t", sellerPhoneOrCode = "0599999999", description = "d", amount = 100m,
            durationValue = 1, durationUnit = "day", needShipping = false, acceptTerms = true
        });
        unknownSeller.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_with_shipping_uses_an_active_company_and_persists_the_selection()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var lookup = await Json(await buyer.Client.GetAsync("/api/v1/lookups/shipping-companies"));
        var companies = lookup.GetProperty("data");
        companies.GetArrayLength().Should().BeGreaterThan(0);
        var shippingCompanyId = companies[0].GetProperty("id").GetInt64();

        var response = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Shipped item", sellerPhoneOrCode = seller.Phone, description = "Delivery to buyer address",
            amount = 250m, durationValue = 3, durationUnit = "day", needShipping = true,
            shippingCompanyId, acceptTerms = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = (await Json(response)).GetProperty("data");
        created.GetProperty("needShipping").GetBoolean().Should().BeTrue();
        created.GetProperty("shippingCompanyId").GetInt64().Should().Be(shippingCompanyId);
        created.GetProperty("shippingAddressId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Created_deal_is_visible_in_the_sellers_deal_list()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await CreateDeal(buyer, seller);

        var response = await seller.Client.GetAsync("/api/v1/deals?page=1&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var deals = (await Json(response)).GetProperty("data").EnumerateArray();
        var deal = deals.Single(item => item.GetProperty("id").GetInt64() == dealId);
        deal.GetProperty("status").GetString().Should().Be("pending_acceptance");
        deal.GetProperty("role").GetString().Should().Be("seller");
    }

    [Fact]
    public async Task Buyer_can_complete_a_fake_payment_and_fund_the_deal_in_development()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await CreateDeal(buyer, seller);
        await Accept(seller, dealId);
        await StartFunding(buyer, dealId);

        var completed = await Post(buyer.Client, $"/api/v1/payments/dev/deals/{dealId}/complete");

        completed.StatusCode.Should().Be(HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());
        (await StatusOf(buyer, dealId)).Should().Be("funded");

        var sellerCannotSimulate = await Post(seller.Client, $"/api/v1/payments/dev/deals/{dealId}/complete");
        sellerCannotSimulate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var delivered = await Post(seller.Client, $"/api/v1/deals/{dealId}/deliver");
        delivered.StatusCode.Should().Be(HttpStatusCode.OK, await delivered.Content.ReadAsStringAsync());
        (await StatusOf(buyer, dealId)).Should().Be("inspection");

        var released = await Post(buyer.Client, $"/api/v1/deals/{dealId}/release", null, Key());
        released.StatusCode.Should().Be(HttpStatusCode.OK, await released.Content.ReadAsStringAsync());
        (await StatusOf(buyer, dealId)).Should().Be("released");
    }

    // ------------------------------------------------------------------ 4. dispute freezes release
    [Fact]
    public async Task Dispute_freezes_escrow_and_blocks_every_release_even_after_the_window()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller, hours: 24);

        var dispute = await Post(buyer.Client, $"/api/v1/deals/{id}/dispute", new { reasonId = DisputeReason, details = "Item not as described" });
        dispute.StatusCode.Should().Be(HttpStatusCode.Created, await dispute.Content.ReadAsStringAsync());
        (await StatusOf(buyer, id)).Should().Be("disputed");
        (await Db(db => db.EscrowHolds.AsNoTracking().Where(h => h.DealId == id).Select(h => h.Status).FirstAsync()))
            .Should().Be(EscrowStatus.FrozenDispute);

        factory.Clock.Advance(TimeSpan.FromHours(30));
        try
        {
            using var scope = factory.Services.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var due = (await sender.Send(new GetDueDealsQuery())).Value;
            due.ReleaseDealIds.Should().NotContain(id, "a disputed deal is never due for automatic release");

            var auto = await sender.Send(new AutoReleaseDealCommand(id));
            auto.IsFailure.Should().BeTrue();
            auto.Error.Code.Should().Be("DEAL_DISPUTED");

            var manual = await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());
            manual.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await ErrorCode(manual)).Should().Be("DEAL_DISPUTED");
        }
        finally { factory.Clock.Reset(); }

        (await LedgerOf(id)).Transactions.Should().NotContain(t => t.Type == LedgerTransactionType.EscrowRelease);
        (await SellerWallet(id)).Should().Be(0m);
    }

    [Fact]
    public async Task Undisputed_deal_is_released_automatically_after_the_inspection_window()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller, hours: 24);

        factory.Clock.Advance(TimeSpan.FromHours(25));
        try
        {
            using var scope = factory.Services.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            (await sender.Send(new GetDueDealsQuery())).Value.ReleaseDealIds.Should().Contain(id);
            (await sender.Send(new AutoReleaseDealCommand(id))).IsSuccess.Should().BeTrue();
            // Running the sweep again must not pay the seller twice.
            (await sender.Send(new AutoReleaseDealCommand(id))).IsFailure.Should().BeTrue();
        }
        finally { factory.Clock.Reset(); }

        (await StatusOf(buyer, id)).Should().Be("released");
        var ledger = await LedgerOf(id);
        ledger.Transactions.Count(t => t.Type == LedgerTransactionType.EscrowRelease).Should().Be(1);
        AssertBalanced(ledger);
        (await SellerWallet(id)).Should().Be(950m);
    }

    [Fact]
    public async Task Auto_release_is_not_due_while_inspection_is_still_running()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller, hours: 24);

        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        (await sender.Send(new GetDueDealsQuery())).Value.ReleaseDealIds.Should().NotContain(id);
        (await sender.Send(new AutoReleaseDealCommand(id))).Error.Code.Should().Be("DEAL_INSPECTION_NOT_ENDED");
    }

    // ------------------------------------------------------------------ listing
    [Fact]
    public async Task My_deals_are_grouped_by_tab_and_role_with_counterparty_data()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var pending = await CreateDeal(buyer, seller, 200m);
        var inProgress = await FundedDeal(buyer, seller, 300m);
        var completed = await InspectionDeal(buyer, seller, 400m);
        await Post(buyer.Client, $"/api/v1/deals/{completed}/release", null, Key());

        async Task<JsonElement> List(HttpClient c, string query) => (await Json(await c.GetAsync("/api/v1/deals" + query))).GetProperty("data");
        static IEnumerable<long> Ids(JsonElement items) => items.EnumerateArray().Select(i => i.GetProperty("id").GetInt64());

        Ids(await List(buyer.Client, "?tab=new")).Should().BeEquivalentTo(new[] { pending });
        Ids(await List(buyer.Client, "?tab=in_progress")).Should().BeEquivalentTo(new[] { inProgress });
        Ids(await List(buyer.Client, "?tab=completed")).Should().BeEquivalentTo(new[] { completed });
        Ids(await List(buyer.Client, "?role=buyer")).Should().HaveCount(3);
        Ids(await List(buyer.Client, "?role=seller")).Should().BeEmpty();
        Ids(await List(seller.Client, "?role=seller")).Should().HaveCount(3);

        var first = (await List(buyer.Client, "?tab=new")).EnumerateArray().First();
        first.GetProperty("role").GetString().Should().Be("buyer");
        first.GetProperty("amount").GetDecimal().Should().Be(200m);
        first.GetProperty("counterpartyName").GetString().Should().StartWith("Deal User");

        (await buyer.Client.GetAsync("/api/v1/deals?tab=bogus")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ------------------------------------------------------------------ global ledger invariant
    [Fact]
    public async Task Every_ledger_transaction_in_the_database_balances()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await InspectionDeal(buyer, seller, 750m);
        await Post(buyer.Client, $"/api/v1/deals/{id}/release", null, Key());

        var (debits, credits) = await Db(async db =>
        {
            var entries = await db.LedgerEntries.AsNoTracking().ToListAsync();
            return (entries.Where(e => e.Direction == LedgerDirection.Debit).Sum(e => e.Amount),
                    entries.Where(e => e.Direction == LedgerDirection.Credit).Sum(e => e.Amount));
        });
        debits.Should().Be(credits);
        debits.Should().BeGreaterThan(0m);

        // Escrow is fully drained for this deal: what came in 750 went out as 700 + 50 fee.
        AssertBalanced(await LedgerOf(id));
        (await SellerWallet(id)).Should().Be(700m);
    }
}
