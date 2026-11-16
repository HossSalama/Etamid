using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Infrastructure.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Disputes per the 16 Nov 2026 design decision: only the buyer opens a dispute
/// (with up to 5 attachments), the two parties never message each other — each
/// side talks to the admin only — the admin never decides a dispute, the admin
/// only executes what the buyer and the seller agreed, requests information
/// from the buyer, and sends the documents to the buyer for court when they
/// cannot agree. Money still moves through the escrow ledger when an agreement
/// is executed (full release / full refund / partial split).
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

    private Task<AdminClient> AdminOperator()
        => AdminTestKit.CreateAsync(factory, "admin.disputes.view", "admin.disputes.decide");

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (key is not null) request.Headers.Add("X-Idempotency-Key", key);
        return await c.SendAsync(request);
    }

    private static string Key() => Guid.NewGuid().ToString("N");

    private static async Task<long> CreateDeal(Party buyer, Party seller, decimal amount = 1000m, int hours = 24)
    {
        var r = await Post(seller.Client, "/api/v1/deals", new
        {
            title = "Used phone", buyerPhoneOrCode = buyer.Phone, description = "iPhone in good condition",
            amount, durationValue = hours, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private static async Task Accept(Party buyer, long dealId)
        => (await Post(buyer.Client, $"/api/v1/deals/{dealId}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);

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
        await Accept(buyer, id);
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

    private static async Task<long> OpenDispute(Party opener, long dealId, IReadOnlyList<string>? filePublicIds = null)
    {
        var r = await Post(opener.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "The item arrived damaged.", filePublicIds });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    // A real 1x1 PNG, so the file sniffer detects an image (not just the extension).
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8Xw8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    private static async Task<string> UploadDisputeFile(Party who)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(PngBytes), "file", "damage.png");
        content.Add(new StringContent("dispute"), "purpose");
        var r = await who.Client.PostAsync("/api/v1/uploads", content);
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("fileId").GetString()!;
    }

    // The admin uploads a dispute document (no app-user owner; purpose fixed to dispute).
    private static async Task<string> UploadAdminFile(AdminClient admin)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(PngBytes), "file", "court-documents.png");
        var r = await admin.Client.PostAsync("/api/v1/admin/uploads", content);
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("fileId").GetString()!;
    }

    private Task<decimal?> WalletBalanceOf(long userId) => Db(db =>
        db.Wallets.AsNoTracking().Where(w => w.UserId == userId).Select(w => (decimal?)w.AvailableBalance).FirstOrDefaultAsync());

    private Task<int> DisputeTransactionCount(long dealId) => Db(db =>
        db.LedgerTransactions.AsNoTracking().CountAsync(t => t.DealId == dealId && t.IdempotencyKey == "escrow-dispute:" + dealId));

    private static async Task<string> DealStatusOf(Party who, long dealId)
        => (await Data(await who.Client.GetAsync($"/api/v1/deals/{dealId}"))).GetProperty("status").GetString()!;

    private static async Task<long> DisputeIdOf(Party who, long dealId)
        => (await Data(await who.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"))).GetProperty("id").GetInt64();

    private static async Task<JsonElement> AdminDisputeDetail(AdminClient admin, long disputeId)
        => await Data(await admin.Client.GetAsync($"/api/v1/admin/disputes/{disputeId}"));

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Seller_cannot_open_dispute_only_the_buyer_can()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);

        var sellerTry = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "The seller cannot open a dispute." });
        sellerTry.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(sellerTry)).Should().Be("DEAL_ACTION_NOT_ALLOWED");

        // Nothing opened: the buyer still sees no dispute on the deal.
        var none = await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute");
        none.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(none)).Should().Be("DISPUTE_NOT_FOUND");

        var disputeId = await OpenDispute(buyer, dealId);

        var detail = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        detail.GetProperty("id").GetInt64().Should().Be(disputeId);
        detail.GetProperty("number").GetString().Should().Be($"DSP-{dealId:D6}");
        detail.GetProperty("status").GetString().Should().Be("under_review");
        detail.GetProperty("openedById").GetInt64().Should().Be(await UserIdOf(buyer.Phone));
        detail.GetProperty("againstUserId").GetInt64().Should().Be(await UserIdOf(seller.Phone));
        detail.GetProperty("role").GetString().Should().Be("buyer");
        (await DealStatusOf(buyer, dealId)).Should().Be("disputed");
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

        var again = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "Trying to open a second dispute." });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(again)).Should().Be("DEAL_DISPUTED");

        var sellerTry = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "The seller still cannot open." });
        sellerTry.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(sellerTry)).Should().Be("DEAL_ACTION_NOT_ALLOWED");
    }

    [Fact]
    public async Task Buyer_opens_dispute_with_an_attachment()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);

        var fileId = await UploadDisputeFile(buyer);
        var disputeId = await OpenDispute(buyer, dealId, new[] { fileId });

        var detail = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        detail.GetProperty("attachments").GetArrayLength().Should().Be(1);
        detail.GetProperty("attachments")[0].GetProperty("fileName").GetString().Should().Be("damage.png");

        // The attachment is linked to the real dispute id (not a temporary EF key).
        var linked = await Db(db => db.DisputeAttachments.AsNoTracking()
            .CountAsync(a => a.DisputeId == disputeId));
        linked.Should().Be(1);
    }

    [Fact]
    public async Task Opening_with_six_files_is_rejected()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);

        var six = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid().ToString()).ToArray();
        var r = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "Too many files.", filePublicIds = six });
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(r)).Should().Be("INVALID_VALUE");

        // Nothing opened.
        var none = await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute");
        none.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(none)).Should().Be("DISPUTE_NOT_FOUND");
    }

    [Fact]
    public async Task Messages_are_visible_only_to_the_buyer_and_the_admin_the_seller_sees_the_reason_and_details()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        var disputeId = await OpenDispute(buyer, dealId);

        var buyerMessage = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "The screen is cracked." });
        buyerMessage.StatusCode.Should().Be(HttpStatusCode.Created, await buyerMessage.Content.ReadAsStringAsync());
        (await Data(buyerMessage)).GetProperty("senderType").GetString().Should().Be("buyer");

        // The thread is buyer ↔ admin only: the seller cannot post in it.
        var sellerMessage = await Post(seller.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "It was fine when I shipped it." });
        sellerMessage.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(sellerMessage)).Should().Be("DEAL_ACTION_NOT_ALLOWED");

        // The admin mediates in the thread: a visible message reaches the buyer,
        // an internal note reaches nobody but the admins.
        var admin = await AdminOperator();

        var adminMessage = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/messages",
            new { message = "Please send photos of the damage.", isInternalNote = false });
        adminMessage.StatusCode.Should().Be(HttpStatusCode.Created, await adminMessage.Content.ReadAsStringAsync());
        (await Data(adminMessage)).GetProperty("senderType").GetString().Should().Be("admin");

        var internalNote = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/messages",
            new { message = "Check the delivery photos.", isInternalNote = true });
        internalNote.StatusCode.Should().Be(HttpStatusCode.Created, await internalNote.Content.ReadAsStringAsync());
        (await Data(internalNote)).GetProperty("isInternalNote").GetBoolean().Should().BeTrue();

        // The buyer sees only its own message plus the admin's visible one —
        // never the internal note.
        var buyerView = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        buyerView.GetProperty("messages").GetArrayLength().Should().Be(2);
        buyerView.GetProperty("messages")[0].GetProperty("senderType").GetString().Should().Be("buyer");
        buyerView.GetProperty("messages")[1].GetProperty("senderType").GetString().Should().Be("admin");

        // The seller sees no thread — no messages, no attachments, no
        // actions — but he does see what the dispute is about: the reason
        // and the details (Shahd, 16 Nov 2026).
        var sellerView = await Data(await seller.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        sellerView.GetProperty("role").GetString().Should().Be("seller");
        sellerView.GetProperty("messages").GetArrayLength().Should().Be(0);
        sellerView.GetProperty("attachments").GetArrayLength().Should().Be(0);
        sellerView.GetProperty("availableActions").GetArrayLength().Should().Be(0);
        sellerView.GetProperty("details").GetString().Should().Be("The item arrived damaged.");
        sellerView.GetProperty("reasonId").GetInt64().Should().Be(DisputeReason);
        sellerView.GetProperty("reasonNameAr").GetString().Should().Be("لم أستلم المنتج");
        sellerView.GetProperty("reasonNameEn").GetString().Should().Be("I did not receive the product");
        sellerView.GetProperty("number").GetString().Should().Be($"DSP-{dealId:D6}");
        sellerView.GetProperty("status").GetString().Should().Be("under_review");

        // The admin sees everything, including the internal note, attributed to no app user.
        var adminView = await AdminDisputeDetail(admin, disputeId);
        adminView.GetProperty("messages").GetArrayLength().Should().Be(3);
        adminView.GetProperty("messages")[2].GetProperty("isInternalNote").GetBoolean().Should().BeTrue();
        adminView.GetProperty("messages")[2].GetProperty("senderName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Admin_request_info_then_execute_agreement_full_refund()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var admin = await AdminOperator();
        var disputeId = await DisputeIdOf(buyer, dealId);

        var info = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/request-info",
            new { note = "Please upload a photo of the damage.", deadlineDays = 3 });
        info.StatusCode.Should().Be(HttpStatusCode.OK, await info.Content.ReadAsStringAsync());
        (await Data(info)).GetProperty("status").GetString().Should().Be("awaiting_info");

        // A party answering moves the dispute back to review.
        var reply = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute/messages",
            new { message = "Photo attached." });
        reply.StatusCode.Should().Be(HttpStatusCode.Created, await reply.Content.ReadAsStringAsync());
        var afterReply = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        afterReply.GetProperty("status").GetString().Should().Be("under_review");

        // The admin executes what the parties agreed — the note records their agreement.
        var executed = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/execute-agreement",
            new { resolution = "RefundBuyer", note = "The buyer and the seller agreed on a full refund." });
        executed.StatusCode.Should().Be(HttpStatusCode.OK, await executed.Content.ReadAsStringAsync());
        var execution = await Data(executed);
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

        var detail = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        detail.GetProperty("status").GetString().Should().Be("closed");
        detail.GetProperty("resolution").GetString().Should().Be("refund_buyer");
        detail.GetProperty("messages")[2].GetProperty("message").GetString().Should().Contain("Agreement between the parties");
    }

    [Fact]
    public async Task Admin_execute_agreement_partial_split_executes_once_and_second_execution_conflicts()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var admin = await AdminOperator();
        var disputeId = await DisputeIdOf(buyer, dealId);

        var executed = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/execute-agreement",
            new { resolution = "Partial", sellerPercent = 60, note = "The parties agreed on a 60/40 split." });
        executed.StatusCode.Should().Be(HttpStatusCode.OK, await executed.Content.ReadAsStringAsync());
        var execution = await Data(executed);
        execution.GetProperty("sellerAmount").GetDecimal().Should().Be(550m);
        execution.GetProperty("buyerAmount").GetDecimal().Should().Be(400m);

        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().Be(550m);
        (await WalletBalanceOf(await UserIdOf(buyer.Phone))).Should().Be(400m);
        (await DisputeTransactionCount(dealId)).Should().Be(1);

        var replay = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/execute-agreement",
            new { resolution = "Partial", sellerPercent = 60, note = "The parties agreed on a 60/40 split." });
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(replay)).Should().Be("DISPUTE_ALREADY_RESOLVED");
        (await DisputeTransactionCount(dealId)).Should().Be(1);
        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().Be(550m);
    }

    [Fact]
    public async Task Admin_execute_agreement_full_release_executes()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var admin = await AdminOperator();
        var disputeId = await DisputeIdOf(buyer, dealId);

        var executed = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/execute-agreement",
            new { resolution = "ReleaseSeller", note = "The buyer and the seller agreed the seller keeps the amount." });
        executed.StatusCode.Should().Be(HttpStatusCode.OK, await executed.Content.ReadAsStringAsync());
        var execution = await Data(executed);
        execution.GetProperty("resolution").GetString().Should().Be("release_seller");
        execution.GetProperty("sellerAmount").GetDecimal().Should().Be(950m);
        execution.GetProperty("buyerAmount").GetDecimal().Should().Be(0m);
        execution.GetProperty("dealStatus").GetString().Should().Be("released");

        (await WalletBalanceOf(await UserIdOf(seller.Phone))).Should().Be(950m);
        (await WalletBalanceOf(await UserIdOf(buyer.Phone))).Should().BeNull();
        (await DealStatusOf(buyer, dealId)).Should().Be("released");
    }

    [Fact]
    public async Task Admin_send_documents_keeps_dispute_open_and_escrow_frozen()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var dealId = await InspectionDeal(buyer, seller);
        await OpenDispute(buyer, dealId);

        var admin = await AdminOperator();
        var disputeId = await DisputeIdOf(buyer, dealId);

        // The admin uploads the court documents, then sends them: the files are
        // attached to an admin message with no text — no sentence, no note.
        var fileId = await UploadAdminFile(admin);
        var sent = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/send-documents",
            new { filePublicIds = new[] { fileId } });
        sent.StatusCode.Should().Be(HttpStatusCode.OK, await sent.Content.ReadAsStringAsync());
        (await Data(sent)).GetProperty("status").GetString().Should().Be("under_review");

        // The buyer sees an admin message with the file and no text; the seller sees nothing.
        var buyerView = await Data(await buyer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        buyerView.GetProperty("messages").GetArrayLength().Should().Be(1);
        buyerView.GetProperty("messages")[0].GetProperty("message").ValueKind.Should().Be(JsonValueKind.Null);
        buyerView.GetProperty("messages")[0].GetProperty("attachments").GetArrayLength().Should().Be(1);
        buyerView.GetProperty("messages")[0].GetProperty("attachments")[0].GetProperty("fileName").GetString().Should().Be("court-documents.png");

        var sellerView = await Data(await seller.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"));
        sellerView.GetProperty("messages").GetArrayLength().Should().Be(0);

        (await DisputeTransactionCount(dealId)).Should().Be(0);
        (await DealStatusOf(buyer, dealId)).Should().Be("disputed");

        // Once the agreement is executed and the dispute closes, sending documents is refused.
        var executed = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/execute-agreement",
            new { resolution = "RefundBuyer", note = "The buyer and the seller agreed on a full refund." });
        executed.StatusCode.Should().Be(HttpStatusCode.OK, await executed.Content.ReadAsStringAsync());

        var again = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/send-documents",
            new { filePublicIds = new[] { fileId } });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(again)).Should().Be("DISPUTE_ALREADY_RESOLVED");
    }

    [Fact]
    public async Task Non_participant_cannot_see_or_message_dispute_and_non_operator_cannot_execute_agreement()
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
        ErrorCode(await Json(message)).Should().Be("DEAL_NOT_FOUND");

        var disputeId = await DisputeIdOf(buyer, dealId);
        var execute = await Post(stranger.Client, $"/api/v1/admin/disputes/{disputeId}/execute-agreement",
            new { resolution = "ReleaseSeller", note = "Not an operator." });
        execute.StatusCode.Should().Be(HttpStatusCode.OK);
        ErrorCode(await Json(execute)).Should().Be("ADMIN_FORBIDDEN");
    }
}
