using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.CompanyDeals;
using Etamid.Domain.Admin;
using Etamid.Domain.CompanyDeals;
using Etamid.Domain.Disputes;
using Etamid.Domain.Money;
using Etamid.Infrastructure.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Cycle 9 phase 2: the withdrawal, penalty and dispute operator points are gated by an AdminJwt identity plus a
/// database-checked permission; *ByAdminId carries the real admin id; every write leaves an AuditLog row.
/// </summary>
public class AdminOperatorTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const long DisputeReason = 7;
    private static readonly string[] Ibans =
    [
        "SA0380000000608010167519", "SA6180000000608010167542", "SA7380000000608010167520", "SA6780000000608010167531"
    ];
    private static int _ibanIndex = -1;
    private static int _counter = 12000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static async Task<string> Code(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private static string Key() => Guid.NewGuid().ToString("N");

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05066{n:D5}";
        var email = $"op.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Operator Test " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code = factory.Emails.CodeFor(email) });
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

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private async Task SendWebhook(string providerPaymentId, decimal amount)
    {
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount, currency = "SAR" });
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A deal funded and delivered, so it sits in inspection (amount 1000).</summary>
    private async Task<(Party Buyer, Party Seller, long DealId)> InspectionDeal()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var create = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var id = (await Data(create)).GetProperty("id").GetInt64();

        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var fund = await Post(buyer.Client, $"/api/v1/deals/{id}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        await SendWebhook((await Data(fund)).GetProperty("providerPaymentId").GetString()!, 1000m);
        (await Post(seller.Client, $"/api/v1/deals/{id}/deliver")).StatusCode.Should().Be(HttpStatusCode.OK);
        return (buyer, seller, id);
    }

    private async Task<long> PendingWithdrawal()
    {
        var (buyer, seller, dealId) = await InspectionDeal();
        (await Post(buyer.Client, $"/api/v1/deals/{dealId}/release", null, Key())).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(seller.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);

        var iban = Ibans[Interlocked.Increment(ref _ibanIndex)];
        var bank = await seller.Client.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "Op Seller", iban });
        bank.StatusCode.Should().Be(HttpStatusCode.OK, await bank.Content.ReadAsStringAsync());

        var request = await Post(seller.Client, "/api/v1/me/wallet/withdrawals", new { amount = 400m, idempotencyKey = Key() });
        request.StatusCode.Should().Be(HttpStatusCode.Created, await request.Content.ReadAsStringAsync());
        return (await Data(request)).GetProperty("id").GetInt64();
    }

    private async Task<long> OpenDispute()
    {
        var (buyer, _, dealId) = await InspectionDeal();
        var open = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute",
            new { reasonId = DisputeReason, details = "The item arrived damaged." });
        open.StatusCode.Should().Be(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());
        return (await Data(open)).GetProperty("id").GetInt64();
    }

    private static string Due(int daysFromNow)
        => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)).ToString("yyyy-MM-dd");

    /// <summary>A company-deal penalty in Applied status (10% of 1000, no grace days).</summary>
    private async Task<long> AppliedPenalty()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();

        var create = await creator.Client.PostAsJsonAsync("/api/v1/company-deals", new
        {
            counterpartyPhoneOrCode = counterparty.Phone, title = "Supply agreement", details = "Monthly supply",
            totalAmount = 1000m, startDate = Due(0), approvalSendDaysBefore = 7, approvalResponseDays = 3,
            penaltyPercent = 10m, penaltyGraceDays = 0,
            installments = new[] { new { label = "Only", amount = 1000m, dueDate = Due(0) } }
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var dealId = (await Data(create)).GetProperty("id").GetInt64();

        (await Post(creator.Client, $"/api/v1/company-deals/{dealId}/submit")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(counterparty.Client, $"/api/v1/company-deals/{dealId}/accept", new { acceptTerms = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        factory.Clock.Advance(TimeSpan.FromDays(2));
        try
        {
            using var scope = factory.Services.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new RunCompanyInstallmentMaintenanceCommand());
            result.IsSuccess.Should().BeTrue(result.Error.Code);
            result.Value.PenaltiesAppliedCount.Should().BeGreaterThan(0);
        }
        finally
        {
            factory.Clock.Reset();
        }

        var detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{dealId}"));
        return detail.GetProperty("penalties")[0].GetProperty("id").GetInt64();
    }

    private async Task<long> DisputeIdOf(long dealId, Party viewer)
        => (await Data(await viewer.Client.GetAsync($"/api/v1/deals/{dealId}/dispute"))).GetProperty("id").GetInt64();

    private Task<List<AuditLog>> AuditRows(string action, long entityId) => Db(db =>
        db.AuditLogs.AsNoTracking().Where(a => a.Action == action && a.EntityId == entityId).ToListAsync());

    private async Task AssertAudit(string action, long entityId, long adminId, string before, string after)
    {
        var rows = await AuditRows(action, entityId);
        rows.Should().ContainSingle();
        var row = rows[0];
        row.ActorType.Should().Be(AuditActorType.Admin);
        row.ActorId.Should().Be(adminId);
        row.OldValues.Should().NotBeNull().And.Contain(before);
        row.NewValues.Should().NotBeNull().And.Contain(after);
    }

    // ------------------------------------------------------------------ withdrawals
    [Fact]
    public async Task Withdrawal_approve_and_complete_fill_the_admin_id_and_write_audit_rows()
    {
        var withdrawalId = await PendingWithdrawal();
        var admin = await AdminTestKit.CreateAsync(factory,
            "admin.withdrawals.view", "admin.withdrawals.approve", "admin.withdrawals.complete", "admin.audit.view");

        var list = await admin.Client.GetAsync("/api/v1/admin/withdrawals");
        list.StatusCode.Should().Be(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());

        var approve = await Post(admin.Client, $"/api/v1/admin/withdrawals/{withdrawalId}/approve");
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        (await Db(db => db.WithdrawalRequests.AsNoTracking().Where(w => w.Id == withdrawalId).Select(w => w.ApprovedByAdminId).FirstAsync()))
            .Should().Be(admin.AdminId);
        await AssertAudit("admin.withdrawal.approve", withdrawalId, admin.AdminId, "Pending", "Approved");

        var complete = await Post(admin.Client, $"/api/v1/admin/withdrawals/{withdrawalId}/complete", new { bankReference = "TRF-OP-1" });
        complete.StatusCode.Should().Be(HttpStatusCode.OK, await complete.Content.ReadAsStringAsync());
        await AssertAudit("admin.withdrawal.complete", withdrawalId, admin.AdminId, "Approved", "Completed");

        // Readable through the admin audit endpoint.
        var logs = await Data(await admin.Client.GetAsync("/admin/api/audit-logs?action=admin.withdrawal.approve"));
        logs.EnumerateArray().Any(l => l.GetProperty("entityId").GetInt64() == withdrawalId).Should().BeTrue();
    }

    [Fact]
    public async Task Withdrawal_reject_needs_the_approve_permission_fills_the_admin_id_and_is_audited()
    {
        var withdrawalId = await PendingWithdrawal();
        var admin = await AdminTestKit.CreateAsync(factory, "admin.withdrawals.approve");

        var reject = await Post(admin.Client, $"/api/v1/admin/withdrawals/{withdrawalId}/reject", new { reason = "Details do not match" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());

        (await Db(db => db.WithdrawalRequests.AsNoTracking().Where(w => w.Id == withdrawalId).Select(w => w.ApprovedByAdminId).FirstAsync()))
            .Should().Be(admin.AdminId);
        await AssertAudit("admin.withdrawal.reject", withdrawalId, admin.AdminId, "Pending", "Rejected");
    }

    [Fact]
    public async Task Withdrawal_endpoints_refuse_app_users_and_admins_without_the_permission_and_unauthenticated_callers()
    {
        var withdrawalId = await PendingWithdrawal();
        var appUser = await NewUser();
        var viewOnly = await AdminTestKit.CreateAsync(factory, "admin.withdrawals.view");
        var noPermission = await AdminTestKit.CreateAsync(factory);

        var writes = new[]
        {
            ($"/api/v1/admin/withdrawals/{withdrawalId}/approve", (object)new { }),
            ($"/api/v1/admin/withdrawals/{withdrawalId}/reject", new { reason = "x" }),
            ($"/api/v1/admin/withdrawals/{withdrawalId}/complete", new { bankReference = "X" })
        };
        foreach (var (url, body) in writes)
        {
            (await Post(appUser.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "app user: " + url);
            (await Post(noPermission.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "admin without permission: " + url);
            (await Post(viewOnly.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "view-only admin: " + url);
            (await Post(_anon, url, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no token: " + url);
        }

        (await _anon.GetAsync("/api/v1/admin/withdrawals")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await appUser.Client.GetAsync("/api/v1/admin/withdrawals")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await noPermission.Client.GetAsync("/api/v1/admin/withdrawals")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewOnly.Client.GetAsync("/api/v1/admin/withdrawals")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Db(db => db.WithdrawalRequests.AsNoTracking().Where(w => w.Id == withdrawalId).Select(w => w.Status).FirstAsync()))
            .Should().Be(WithdrawalStatus.Pending);
        (await AuditRows("admin.withdrawal.approve", withdrawalId)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ penalties
    [Fact]
    public async Task Penalty_waive_fills_the_admin_id_and_writes_an_audit_row()
    {
        var penaltyId = await AppliedPenalty();
        var admin = await AdminTestKit.CreateAsync(factory, "admin.penalties.view", "admin.penalties.waive");

        (await admin.Client.GetAsync("/api/v1/admin/company-deals/penalties")).StatusCode.Should().Be(HttpStatusCode.OK);

        var waived = await Post(admin.Client, $"/api/v1/admin/company-deals/penalties/{penaltyId}/waive", new { reason = "Goodwill gesture" });
        waived.StatusCode.Should().Be(HttpStatusCode.OK, await waived.Content.ReadAsStringAsync());

        (await Db(db => db.PenaltyCharges.AsNoTracking().Where(p => p.Id == penaltyId).Select(p => p.WaivedByAdminId).FirstAsync()))
            .Should().Be(admin.AdminId);
        await AssertAudit("admin.penalty.waive", penaltyId, admin.AdminId, "Applied", "Waived");
    }

    [Fact]
    public async Task Penalty_endpoints_refuse_app_users_and_admins_without_the_permission_and_unauthenticated_callers()
    {
        var penaltyId = await AppliedPenalty();
        var appUser = await NewUser();
        var viewOnly = await AdminTestKit.CreateAsync(factory, "admin.penalties.view");
        var noPermission = await AdminTestKit.CreateAsync(factory);
        var url = $"/api/v1/admin/company-deals/penalties/{penaltyId}/waive";
        var body = new { reason = "Goodwill" };

        (await Post(appUser.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(noPermission.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(viewOnly.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Post(_anon, url, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await _anon.GetAsync("/api/v1/admin/company-deals/penalties")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await appUser.Client.GetAsync("/api/v1/admin/company-deals/penalties")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await noPermission.Client.GetAsync("/api/v1/admin/company-deals/penalties")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await Db(db => db.PenaltyCharges.AsNoTracking().Where(p => p.Id == penaltyId).Select(p => p.Status).FirstAsync()))
            .Should().Be(PenaltyStatus.Applied);
        (await AuditRows("admin.penalty.waive", penaltyId)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ disputes
    [Fact]
    public async Task Dispute_request_info_and_decide_fill_the_admin_id_and_write_audit_rows()
    {
        var disputeId = await OpenDispute();
        var admin = await AdminTestKit.CreateAsync(factory, "admin.disputes.view", "admin.disputes.decide");

        (await admin.Client.GetAsync("/api/v1/admin/disputes")).StatusCode.Should().Be(HttpStatusCode.OK);

        var info = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/request-info", new { note = "Please upload a photo.", deadlineDays = 3 });
        info.StatusCode.Should().Be(HttpStatusCode.OK, await info.Content.ReadAsStringAsync());
        await AssertAudit("admin.dispute.request_info", disputeId, admin.AdminId, "UnderReview", "AwaitingInfo");

        var decided = await Post(admin.Client, $"/api/v1/admin/disputes/{disputeId}/decide",
            new { resolution = "RefundBuyer", note = "The item was damaged in transit." });
        decided.StatusCode.Should().Be(HttpStatusCode.OK, await decided.Content.ReadAsStringAsync());

        (await Db(db => db.Disputes.AsNoTracking().Where(d => d.Id == disputeId).Select(d => d.ResolvedByAdminId).FirstAsync()))
            .Should().Be(admin.AdminId);
        await AssertAudit("admin.dispute.decide", disputeId, admin.AdminId, "AwaitingInfo", "Closed");

        // The decision message is attributed to the admin, not to an app user.
        var message = await Db(db => db.DisputeMessages.AsNoTracking()
            .Where(m => m.DisputeId == disputeId && m.SenderType == DisputeSenderType.Admin).OrderByDescending(m => m.Id).FirstAsync());
        message.SenderAdminId.Should().Be(admin.AdminId);
        message.SenderUserId.Should().BeNull();
    }

    [Fact]
    public async Task Dispute_endpoints_refuse_app_users_and_admins_without_the_permission_and_unauthenticated_callers()
    {
        var (buyer, seller, dealId) = await InspectionDeal();
        var open = await Post(buyer.Client, $"/api/v1/deals/{dealId}/dispute", new { reasonId = DisputeReason, details = "Damaged." });
        open.StatusCode.Should().Be(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());
        var disputeId = await DisputeIdOf(dealId, buyer);

        var viewOnly = await AdminTestKit.CreateAsync(factory, "admin.disputes.view");
        var noPermission = await AdminTestKit.CreateAsync(factory);

        var writes = new[]
        {
            ($"/api/v1/admin/disputes/{disputeId}/request-info", (object)new { note = "x", deadlineDays = 3 }),
            ($"/api/v1/admin/disputes/{disputeId}/decide", new { resolution = "ReleaseSeller", note = "x" })
        };
        foreach (var (url, body) in writes)
        {
            (await Post(buyer.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "buyer: " + url);
            (await Post(seller.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "seller: " + url);
            (await Post(noPermission.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "admin without permission: " + url);
            (await Post(viewOnly.Client, url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "view-only admin: " + url);
            (await Post(_anon, url, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "no token: " + url);
        }

        (await _anon.GetAsync("/api/v1/admin/disputes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await buyer.Client.GetAsync("/api/v1/admin/disputes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await noPermission.Client.GetAsync("/api/v1/admin/disputes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewOnly.Client.GetAsync("/api/v1/admin/disputes")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Db(db => db.Disputes.AsNoTracking().Where(d => d.Id == disputeId).Select(d => d.Status).FirstAsync()))
            .Should().Be(DisputeStatus.UnderReview);
        (await AuditRows("admin.dispute.decide", disputeId)).Should().BeEmpty();
    }
}
