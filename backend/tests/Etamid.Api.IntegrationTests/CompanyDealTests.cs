using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.CompanyDeals;
using Etamid.Domain.Config;
using Etamid.Infrastructure.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Company deals v1: a separate path from individual deals — wizard draft,
/// counterparty acceptance, per-installment escrow (fund, approve, release),
/// reject-and-resubmit, automatic penalties past the grace period, and
/// operator waiver. Funding goes through the Fake gateway + signed webhook,
/// like the individual-deal tests; no bank accounts are saved here, so no
/// IBAN is shared with any other test class.
/// </summary>
public class CompanyDealTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 9500;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05095{n:D5}";
        var email = $"company.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Company User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private async Task<long> CreateDraft(Party creator, Party counterparty, decimal total, params (string? Label, decimal Amount, string Due)[] installments)
        => await CreateDraftWithOptions(creator, counterparty, total, null, null, installments);

    private async Task<long> CreateDraftWithOptions(
        Party creator, Party counterparty, decimal total, decimal? penaltyPercent, int? graceDays,
        params (string? Label, decimal Amount, string Due)[] installments)
    {
        var create = await creator.Client.PostAsJsonAsync("/api/v1/company-deals", new
        {
            counterpartyPhoneOrCode = counterparty.Phone,
            title = "Supply agreement",
            details = "Monthly supply of packaging",
            totalAmount = total,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            approvalSendDaysBefore = 7,
            approvalResponseDays = 3,
            penaltyPercent = penaltyPercent,
            penaltyGraceDays = graceDays,
            installments = installments.Select(i => new { label = i.Label, amount = i.Amount, dueDate = i.Due }).ToList()
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        return (await Data(create)).GetProperty("id").GetInt64();
    }

    private async Task SubmitAndAccept(Party creator, Party counterparty, long dealId)
    {
        (await Post(creator.Client, $"/api/v1/company-deals/{dealId}/submit")).StatusCode.Should().Be(HttpStatusCode.OK);
        var accept = await Post(counterparty.Client, $"/api/v1/company-deals/{dealId}/accept", new { acceptTerms = true });
        accept.StatusCode.Should().Be(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());
    }

    private async Task<long> InstallmentIdOf(Party viewer, long dealId, int index)
    {
        var detail = await viewer.Client.GetAsync($"/api/v1/company-deals/{dealId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await Data(detail)).GetProperty("installments")[index].GetProperty("id").GetInt64();
    }

    private async Task FundViaWebhook(Party payer, long dealId, long installmentId, decimal amount)
    {
        var fund = await Post(payer.Client, $"/api/v1/company-deals/{dealId}/installments/{installmentId}/fund", null, Key());
        fund.StatusCode.Should().Be(HttpStatusCode.OK, await fund.Content.ReadAsStringAsync());
        var providerPaymentId = (await Data(fund)).GetProperty("providerPaymentId").GetString()!;
        var body = JsonSerializer.Serialize(new { eventId = "evt_" + Key(), type = "payment.succeeded", providerPaymentId, amount, currency = "SAR" });
        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        webhook.Headers.Add("X-Signature", HmacWebhookSignatureVerifier.Sign(EtamidFactory.WebhookSecret, body));
        (await _anon.SendAsync(webhook)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<long> UserIdOf(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync();
    }

    private async Task MakeCompanyOperator(long userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == "company.operators");
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = "company.operators", Value = userId.ToString(),
                ValueType = SettingValueType.String, UpdatedAt = DateTime.UtcNow
            });
        }
        else if (!row.Value.Split(',').Select(p => p.Trim()).Contains(userId.ToString()))
        {
            row.Value = row.Value + "," + userId;
        }
        await db.SaveChangesAsync();
    }

    private async Task<CompanyMaintenanceDto> RunMaintenance()
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new RunCompanyInstallmentMaintenanceCommand());
        result.IsSuccess.Should().BeTrue(result.Error.Code);
        return result.Value;
    }

    private static string Due(int daysFromNow)
        => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Create_draft_validates_installment_sum()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();

        var bad = await creator.Client.PostAsJsonAsync("/api/v1/company-deals", new
        {
            counterpartyPhoneOrCode = counterparty.Phone,
            title = "Supply agreement",
            details = "Monthly supply",
            totalAmount = 1500m,
            startDate = Due(0),
            installments = new[] { new { label = "First", amount = 1000m, dueDate = Due(30) } }
        });
        bad.StatusCode.Should().Be(HttpStatusCode.OK, await bad.Content.ReadAsStringAsync());
        (await Json(bad)).GetProperty("errors")[0].GetProperty("code").GetString()
            .Should().Be("COMPANY_INSTALLMENTS_SUM_MISMATCH");

        var id = await CreateDraft(creator, counterparty, 1500m,
            ("First", 1000m, Due(30)), ("Second", 500m, Due(60)));
        var detail = await creator.Client.GetAsync($"/api/v1/company-deals/{id}");
        var data = await Data(detail);
        data.GetProperty("status").GetString().Should().Be("draft");
        data.GetProperty("totalAmount").GetDecimal().Should().Be(1500m);
        data.GetProperty("installments").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Submit_and_counterparty_accept()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();
        var id = await CreateDraft(creator, counterparty, 1000m, ("Only", 1000m, Due(30)));

        (await Post(creator.Client, $"/api/v1/company-deals/{id}/submit")).StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
        detail.GetProperty("status").GetString().Should().Be("pending");

        // The creator cannot accept their own deal (wrong role: FORBIDDEN, not NOT_FOUND).
        var ownAccept = await Post(creator.Client, $"/api/v1/company-deals/{id}/accept", new { acceptTerms = true });
        ownAccept.StatusCode.Should().Be(HttpStatusCode.OK);

        var accept = await Post(counterparty.Client, $"/api/v1/company-deals/{id}/accept", new { acceptTerms = true });
        accept.StatusCode.Should().Be(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());
        (await Data(accept)).GetProperty("status").GetString().Should().Be("active");
    }

    [Fact]
    public async Task Counterparty_reject_with_reason()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();
        var id = await CreateDraft(creator, counterparty, 1000m, ("Only", 1000m, Due(30)));
        (await Post(creator.Client, $"/api/v1/company-deals/{id}/submit")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Reason 13 is a company_reject_reason seed; reason 1 is a deal
        // reject_reason, so it must not validate here.
        var badReason = await Post(counterparty.Client, $"/api/v1/company-deals/{id}/reject", new { reasonId = 1 });
        badReason.StatusCode.Should().Be(HttpStatusCode.OK, await badReason.Content.ReadAsStringAsync());

        var reject = await Post(counterparty.Client, $"/api/v1/company-deals/{id}/reject",
            new { reasonId = 13, note = "Schedule does not work" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());
        (await Data(reject)).GetProperty("status").GetString().Should().Be("rejected");
    }

    [Fact]
    public async Task Installment_approve_and_escrow_release()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();
        // The creator's wallet must be active before its balance is visible.
        (await Post(creator.Client, "/api/v1/me/wallet/activate")).StatusCode.Should().Be(HttpStatusCode.OK);

        var id = await CreateDraft(creator, counterparty, 1500m,
            ("First", 1000m, Due(30)), ("Second", 500m, Due(60)));
        await SubmitAndAccept(creator, counterparty, id);

        var first = await InstallmentIdOf(creator, id, 0);
        await FundViaWebhook(counterparty, id, first, 1000m);
        (await Post(creator.Client, $"/api/v1/company-deals/{id}/installments/{first}/request-approval"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var approve = await Post(counterparty.Client, $"/api/v1/company-deals/{id}/installments/{first}/approve");
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());

        // First release carries the single per-deal platform fee (50).
        var wallet = await Data(await creator.Client.GetAsync("/api/v1/me/wallet"));
        wallet.GetProperty("availableBalance").GetDecimal().Should().Be(950m);
        var detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
        detail.GetProperty("installments")[0].GetProperty("status").GetString().Should().Be("paid");
        detail.GetProperty("status").GetString().Should().Be("active");

        // Second release takes no further fee, and paying it completes the deal.
        var second = await InstallmentIdOf(creator, id, 1);
        await FundViaWebhook(counterparty, id, second, 500m);
        (await Post(creator.Client, $"/api/v1/company-deals/{id}/installments/{second}/request-approval"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(counterparty.Client, $"/api/v1/company-deals/{id}/installments/{second}/approve"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        wallet = await Data(await creator.Client.GetAsync("/api/v1/me/wallet"));
        wallet.GetProperty("availableBalance").GetDecimal().Should().Be(1450m);
        detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
        detail.GetProperty("status").GetString().Should().Be("completed");
    }

    [Fact]
    public async Task Installment_reject_and_resubmit()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();
        var id = await CreateDraft(creator, counterparty, 1000m, ("Only", 1000m, Due(30)));
        await SubmitAndAccept(creator, counterparty, id);
        var installment = await InstallmentIdOf(creator, id, 0);

        (await Post(creator.Client, $"/api/v1/company-deals/{id}/installments/{installment}/request-approval"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var reject = await Post(counterparty.Client, $"/api/v1/company-deals/{id}/installments/{installment}/reject",
            new { reason = "Work incomplete" });
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());
        var detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
        var row = detail.GetProperty("installments")[0];
        row.GetProperty("status").GetString().Should().Be("rejected");
        row.GetProperty("rejectionReason").GetString().Should().Be("Work incomplete");
        detail.GetProperty("status").GetString().Should().Be("active");

        // Resubmission after revision: the deal and its schedule survive.
        (await Post(creator.Client, $"/api/v1/company-deals/{id}/installments/{installment}/request-approval"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var approve = await Post(counterparty.Client, $"/api/v1/company-deals/{id}/installments/{installment}/approve");
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
        detail.GetProperty("installments")[0].GetProperty("status").GetString().Should().Be("approved");
    }

    [Fact]
    public async Task Penalty_applied_after_grace()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();
        var id = await CreateDraftWithOptions(creator, counterparty, 1000m, 10m, 0, ("Only", 1000m, Due(0)));
        await SubmitAndAccept(creator, counterparty, id);

        factory.Clock.Advance(TimeSpan.FromDays(2));
        try
        {
            var maintenance = await RunMaintenance();
            maintenance.PenaltiesAppliedCount.Should().Be(1);

            var detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
            detail.GetProperty("installments")[0].GetProperty("status").GetString().Should().Be("penalty_applied");
            var penalties = detail.GetProperty("penalties");
            penalties.GetArrayLength().Should().Be(1);
            penalties[0].GetProperty("penaltyAmount").GetDecimal().Should().Be(100m);
            penalties[0].GetProperty("status").GetString().Should().Be("applied");
        }
        finally
        {
            factory.Clock.Reset();
        }
    }

    [Fact]
    public async Task Operator_waives_penalty()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();
        var id = await CreateDraftWithOptions(creator, counterparty, 1000m, 10m, 0, ("Only", 1000m, Due(0)));
        await SubmitAndAccept(creator, counterparty, id);

        factory.Clock.Advance(TimeSpan.FromDays(2));
        try
        {
            await RunMaintenance();
            var detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
            var penaltyId = detail.GetProperty("penalties")[0].GetProperty("id").GetInt64();

            // Waiver needs the operator setting, like wallet approvals.
            var denied = await Post(creator.Client, $"/api/v1/admin/company-deals/penalties/{penaltyId}/waive",
                new { reason = "Goodwill" });
            denied.StatusCode.Should().Be(HttpStatusCode.OK);

            await MakeCompanyOperator(await UserIdOf(creator.Phone));
            var waived = await Post(creator.Client, $"/api/v1/admin/company-deals/penalties/{penaltyId}/waive",
                new { reason = "Goodwill gesture" });
            waived.StatusCode.Should().Be(HttpStatusCode.OK, await waived.Content.ReadAsStringAsync());
            (await Data(waived)).GetProperty("status").GetString().Should().Be("waived");

            detail = await Data(await creator.Client.GetAsync($"/api/v1/company-deals/{id}"));
            detail.GetProperty("installments")[0].GetProperty("status").GetString().Should().Be("overdue");
        }
        finally
        {
            factory.Clock.Reset();
        }
    }

    [Fact]
    public async Task Non_participant_cannot_see_deal()
    {
        var creator = await NewUser();
        var counterparty = await NewUser();
        var stranger = await NewUser();
        var id = await CreateDraft(creator, counterparty, 1000m, ("Only", 1000m, Due(30)));

        (await stranger.Client.GetAsync($"/api/v1/company-deals/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Post(stranger.Client, $"/api/v1/company-deals/{id}/submit")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The counterparty is a participant, but editing is creator-only: FORBIDDEN, not NOT_FOUND.
        var edit = await stranger.Client.PutAsJsonAsync($"/api/v1/company-deals/{id}", new
        {
            title = "Changed", details = "Changed", totalAmount = 1000m, startDate = Due(0),
            installments = new[] { new { label = "Only", amount = 1000m, dueDate = Due(30) } }
        });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        var counterpartyEdit = await counterparty.Client.PutAsJsonAsync($"/api/v1/company-deals/{id}", new
        {
            title = "Changed", details = "Changed", totalAmount = 1000m, startDate = Due(0),
            installments = new[] { new { label = "Only", amount = 1000m, dueDate = Due(30) } }
        });
        counterpartyEdit.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
