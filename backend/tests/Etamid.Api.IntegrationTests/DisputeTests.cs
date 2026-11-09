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
/// Disputes v1: both parties may open, messages + internal notes, mutual settlement
/// that executes immediately, and admin decisions that execute immediately
/// (full release / full refund / partial split) through the escrow ledger.
/// </summary>
public class DisputeTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const long DisputeReason = 7;
    private static int _counter = 700;

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
        var local = $"05097{n:D5}";
        var email = $"dispute.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Dispute User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private Task<long> UserIdOf(string phone) => Db(db =>
        db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync());

    /// <summary>Appends a user to the disputes.operators setting (single row per key in SQL Server).</summary>
    private async Task MakeOperator(long userId)
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

    private static async Task<long> OpenDispute(Party opener, long dealId)
    {
        var r = await Post(opener.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "The item arrived damaged." });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private Task<decimal?> WalletBalanceOf(long userId) => Db(db =>
        db.Wallets.AsNoTracking().Where(w => w.UserId == userId).Select(w => (decimal?)w.AvailableBalance).FirstOrDefaultAsync());

    private Task<int> DisputeTransactionCount(long dealId) => Db(db =>
        db.LedgerTransactions.AsNoTracking().CountAsync(t => t.DealId == dealId && t.IdempotencyKey == "escrow-dispute:" + dealId));

    private static async Task<string> DealStatusOf(Party who, long dealId)
        => (await Data(await who.Client.GetAsync($"/api/v1/deals/{dealId}"))).GetProperty("status").GetString()!;

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Seller_opens_dispute_against_buyer()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);

        var disputeId = await OpenDispute(seller, dealId);

        var detail = await Data(await seller.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        detail.GetProperty("id").GetInt64().Should().Be(disputeId);
        detail.GetProperty("number").GetString().Should().Be($"DSP-{dealId:D6}");
        detail.GetProperty("status").GetString().Should().Be("under_review");
        detail.GetProperty("openedById").GetInt64().Should().Be(await UserIdOf(seller.Phone));
        detail.GetProperty("againstUserId").GetInt64().Should().Be(await UserIdOf(buyer.Phone));
        detail.GetProperty("role").GetString().Should().Be("seller");
        (await DealStatusOf(seller, dealId)).Should().Be("disputed");
    }

    [Fact]
    public async Task Buyer_opens_dispute_against_seller_and_second_open_conflicts()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);

        await OpenDispute(buyer, dealId);

        var detail = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        detail.GetProperty("openedById").GetInt64().Should().Be(await UserIdOf(buyer.Phone));
        detail.GetProperty("againstUserId").GetInt64().Should().Be(await UserIdOf(seller.Phone));

        var again = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "Trying to open a second dispute." });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(again)).Should().Be("DEAL_DISPUTED");
    }

    [Fact]
    public async Task Messages_by_both_parties_and_operator_internal_note_hidden_from_parties()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var buyerMessage = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "The screen is cracked." });
        buyerMessage.StatusCode.Should().Be(HttpStatusCode.Created, await buyerMessage.Content.ReadAsStringAsync());
        (await Data(buyerMessage)).GetProperty("senderType").GetString().Should().Be("buyer");

        var sellerMessage = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "It was fine when I shipped it." });
        sellerMessage.StatusCode.Should().Be(HttpStatusCode.Created, await sellerMessage.Content.ReadAsStringAsync());
        (await Data(sellerMessage)).GetProperty("senderType").GetString().Should().Be("seller");

        var operatorUser = await NewUser();
        await MakeOperator(await UserIdOf(operatorUser.Phone));
        var internalNote = await Post(operatorUser.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "Check the delivery photos.", isInternalNote = true });
        internalNote.StatusCode.Should().Be(HttpStatusCode.Created, await internalNote.Content.ReadAsStringAsync());
        (await Data(internalNote)).GetProperty("isInternalNote").GetBoolean().Should().BeTrue();

        var partyView = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        partyView.GetProperty("messages").GetArrayLength().Should().Be(2);

        var operatorView = await Data(await operatorUser.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        operatorView.GetProperty("messages").GetArrayLength().Should().Be(3);
        operatorView.GetProperty("messages")[2].GetProperty("isInternalNote").GetBoolean().Should().BeTrue();
        operatorView.GetProperty("role").GetString().Should().Be("operator");
    }

    [Fact]
    public async Task Settlement_propose_and_accept_executes_split()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(seller, dealId);

        var proposed = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute/settlement/propose",
            new { resolution = "Partial", sellerPercent = 60 });
        proposed.StatusCode.Should().Be(HttpStatusCode.Created, await proposed.Content.ReadAsStringAsync());
        var proposalId = (await Data(proposed)).GetProperty("id").GetInt64();

        var accepted = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/settlement/{proposalId}/accept");
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        var execution = await Data(accepted);
        execution.GetProperty("resolution").GetString().Should().Be("partial");
        execution.GetProperty("sellerAmount").GetDecimal().Should().Be(550m);
        execution.GetProperty("buyerAmount").GetDecimal().Should().Be(400m);
        execution.GetProperty("feeAmount").GetDecimal().Should().Be(50m);
        execution.GetProperty("dealStatus").GetString().Should().Be("released");

        // 1000 split 60/40, the one 50 fee comes off the seller's share.
        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().Be(550m);
        (await WalletBalanceOf(await UserIdOf(buyer.Phone))).Should().Be(400m);
        (await DisputeTransactionCount(dealId)).Should().Be(1);
        (await DealStatusOf(buyer, dealId)).Should().Be("released");

        var detail = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        detail.GetProperty("status").GetString().Should().Be("closed");
        detail.GetProperty("resolution").GetString().Should().Be("partial");
    }

    [Fact]
    public async Task Settlement_reject_keeps_dispute_open_and_allows_a_new_proposal()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var proposed = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/settlement/propose",
            new { resolution = "RefundBuyer" });
        proposed.StatusCode.Should().Be(HttpStatusCode.Created, await proposed.Content.ReadAsStringAsync());
        var proposalId = (await Data(proposed)).GetProperty("id").GetInt64();

        var rejected = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute/settlement/{proposalId}/reject");
        rejected.StatusCode.Should().Be(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync());
        (await Data(rejected)).GetProperty("status").GetString().Should().Be("rejected");

        var detail = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        detail.GetProperty("status").GetString().Should().Be("under_review");
        detail.GetProperty("openProposal").ValueKind.Should().Be(JsonValueKind.Null);

        var again = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/settlement/propose",
            new { resolution = "Partial", sellerPercent = 50 });
        again.StatusCode.Should().Be(HttpStatusCode.Created, await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_request_info_then_decide_full_refund_executes()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var operatorUser = await NewUser();
        await MakeOperator(await UserIdOf(operatorUser.Phone));
        var disputeId = (await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"))).GetProperty("id").GetInt64();

        var info = await Post(operatorUser.Client, $"/api/v1/admin/disputes/{disputeId}/request-info",
            new { note = "Please upload a photo of the damage.", deadlineDays = 3 });
        info.StatusCode.Should().Be(HttpStatusCode.OK, await info.Content.ReadAsStringAsync());
        (await Data(info)).GetProperty("status").GetString().Should().Be("awaiting_info");

        // A party answering moves the dispute back to review.
        var reply = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "Photo attached." });
        reply.StatusCode.Should().Be(HttpStatusCode.Created, await reply.Content.ReadAsStringAsync());
        var afterReply = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        afterReply.GetProperty("status").GetString().Should().Be("under_review");

        var decided = await Post(operatorUser.Client, $"/api/v1/admin/disputes/{disputeId}/decide",
            new { resolution = "RefundBuyer", note = "The item was damaged in transit." });
        decided.StatusCode.Should().Be(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());
        var execution = await Data(decided);
        execution.GetProperty("resolution").GetString().Should().Be("refund_buyer");
        execution.GetProperty("sellerAmount").GetDecimal().Should().Be(0m);
        execution.GetProperty("buyerAmount").GetDecimal().Should().Be(1000m);
        execution.GetProperty("feeAmount").GetDecimal().Should().Be(50m);
        execution.GetProperty("dealStatus").GetString().Should().Be("refunded");

        // Full refund: the buyer gets the whole funded amount back, no fee is taken.
        (await WalletBalanceOf(await UserIdOf(buyer.Phone))).Should().Be(1000m);
        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().BeNull();
        (await DisputeTransactionCount(dealId)).Should().Be(1);
        (await DealStatusOf(buyer, dealId)).Should().Be("refunded");
    }

    [Fact]
    public async Task Admin_decide_partial_split_executes_once_and_second_decide_conflicts()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var operatorUser = await NewUser();
        await MakeOperator(await UserIdOf(operatorUser.Phone));
        var disputeId = (await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"))).GetProperty("id").GetInt64();

        var decided = await Post(operatorUser.Client, $"/api/v1/admin/disputes/{disputeId}/decide",
            new { resolution = "Partial", sellerPercent = 60, note = "Split by usage." });
        decided.StatusCode.Should().Be(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());
        var execution = await Data(decided);
        execution.GetProperty("sellerAmount").GetDecimal().Should().Be(550m);
        execution.GetProperty("buyerAmount").GetDecimal().Should().Be(400m);

        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().Be(550m);
        (await WalletBalanceOf(await UserIdOf(buyer.Phone))).Should().Be(400m);
        (await DisputeTransactionCount(dealId)).Should().Be(1);

        var replay = await Post(operatorUser.Client, $"/api/v1/admin/disputes/{disputeId}/decide",
            new { resolution = "Partial", sellerPercent = 60, note = "Split by usage." });
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(replay)).Should().Be("DISPUTE_ALREADY_RESOLVED");
        (await DisputeTransactionCount(dealId)).Should().Be(1);
        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().Be(550m);
    }

    [Fact]
    public async Task Admin_decide_full_release_executes()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var operatorUser = await NewUser();
        await MakeOperator(await UserIdOf(operatorUser.Phone));
        var disputeId = (await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"))).GetProperty("id").GetInt64();

        var decided = await Post(operatorUser.Client, $"/api/v1/admin/disputes/{disputeId}/decide",
            new { resolution = "ReleaseSeller", note = "Delivery confirmed." });
        decided.StatusCode.Should().Be(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());
        var execution = await Data(decided);
        execution.GetProperty("resolution").GetString().Should().Be("release_seller");
        execution.GetProperty("sellerAmount").GetDecimal().Should().Be(950m);
        execution.GetProperty("buyerAmount").GetDecimal().Should().Be(0m);
        execution.GetProperty("dealStatus").GetString().Should().Be("released");

        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().Be(950m);
        (await WalletBalanceOf(await UserIdOf(buyer.Phone))).Should().BeNull();
        (await DealStatusOf(buyer, dealId)).Should().Be("released");
    }

    [Fact]
    public async Task Non_participant_cannot_see_or_message_dispute_and_non_operator_cannot_decide()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var stranger = await NewUser();
        var detail = await stranger.Client.GetAsync($"/api/v1/deals/{dealId}/dispute");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(detail)).Should().Be("DEAL_NOT_FOUND");

        var message = await Post(stranger.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "Let me in." });
        message.StatusCode.Should().Be(HttpStatusCode.OK);

        var disputeId = (await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"))).GetProperty("id").GetInt64();
        var decide = await Post(stranger.Client, $"/api/v1/admin/disputes/{disputeId}/decide",
            new { resolution = "ReleaseSeller", note = "Not an operator." });
        decide.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(decide)).Should().Be("DISPUTE_NOT_OPERATOR");
    }
}
