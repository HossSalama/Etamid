using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Cycle 3 completion: cancel, edit propose/approve/reject, and seller lookup.
/// These tests were written against the new endpoints but could not be executed
/// in the review environment (no dotnet SDK there) — run `dotnet test` before merging.
/// </summary>
public class DealsCycle3CompletionTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 500;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<string> ErrorCode(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05044{n:D5}";
        var email = $"c3c.{n}@example.com";

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

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        return await c.SendAsync(request);
    }

    private static async Task<long> CreateDeal(Party buyer, Party seller, decimal amount = 1000m)
    {
        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private static async Task<JsonElement> Detail(Party who, long dealId)
        => (await Json(await who.Client.GetAsync($"/api/v1/deals/{dealId}"))).GetProperty("data");

    private static async Task<string> StatusOf(Party who, long dealId)
        => (await Detail(who, dealId)).GetProperty("status").GetString()!;

    [Fact]
    public async Task Buyer_can_cancel_before_acceptance()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);

        var r = await Post(buyer.Client, $"/api/v1/deals/{id}/cancel");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        (await StatusOf(buyer, id)).Should().Be("cancelled");
    }

    [Fact]
    public async Task Seller_can_cancel_after_acceptance_but_before_funding()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);
        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var r = await Post(seller.Client, $"/api/v1/deals/{id}/cancel");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        (await StatusOf(buyer, id)).Should().Be("cancelled");
    }

    [Fact]
    public async Task Stranger_cannot_cancel()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var stranger = await NewUser();
        var id = await CreateDeal(buyer, seller);

        var r = await Post(stranger.Client, $"/api/v1/deals/{id}/cancel");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCode(r)).Should().Be("DEAL_NOT_FOUND");
    }

    [Fact]
    public async Task Seller_lookup_by_phone_returns_minimal_preview()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var r = await buyer.Client.GetAsync($"/api/v1/users/lookup?q={seller.Phone}");
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var data = (await Json(r)).GetProperty("data");
        data.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        data.TryGetProperty("phone", out _).Should().BeFalse("lookup must not leak the phone number");
        data.TryGetProperty("email", out _).Should().BeFalse("lookup must not leak the email address");
        data.TryGetProperty("id", out _).Should().BeFalse("lookup must not leak the user id");
    }

    [Fact]
    public async Task Seller_lookup_excludes_self_and_unknown()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        _ = seller;

        var self = await buyer.Client.GetAsync($"/api/v1/users/lookup?q={buyer.Phone}");
        self.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCode(self)).Should().Be("SELLER_NOT_FOUND");

        var unknown = await buyer.Client.GetAsync("/api/v1/users/lookup?q=0599999999");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_proposed_by_seller_and_approved_by_buyer_updates_terms()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);

        var propose = await Post(seller.Client, $"/api/v1/deals/{id}/edits", new { amount = 900m });
        propose.StatusCode.Should().Be(HttpStatusCode.Created, await propose.Content.ReadAsStringAsync());
        var editId = (await Json(propose)).GetProperty("data").GetProperty("id").GetInt64();

        var detail = await Detail(buyer, id);
        detail.GetProperty("status").GetString().Should().Be("pending_acceptance");
        detail.GetProperty("pendingEdit").ValueKind.Should().NotBe(JsonValueKind.Null);
        detail.GetProperty("pendingEdit").GetProperty("newAmount").GetDecimal().Should().Be(900m);
        detail.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString())
            .Should().Contain("approve_edit");

        // The proposer cannot approve their own edit.
        var selfApprove = await Post(seller.Client, $"/api/v1/deals/{id}/edits/{editId}/approve");
        selfApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var approve = await Post(buyer.Client, $"/api/v1/deals/{id}/edits/{editId}/approve");
        approve.StatusCode.Should().Be(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());

        var after = await Detail(buyer, id);
        after.GetProperty("amount").GetDecimal().Should().Be(900m);
        after.GetProperty("status").GetString().Should().Be("pending_acceptance");
        after.GetProperty("pendingEdit").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Second_pending_edit_is_rejected_and_rejected_edit_changes_nothing()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);

        (await Post(seller.Client, $"/api/v1/deals/{id}/edits", new { amount = 900m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Post(buyer.Client, $"/api/v1/deals/{id}/edits", new { amount = 800m });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCode(second)).Should().Be("DEAL_EDIT_ALREADY_PENDING");

        var detail = await Detail(buyer, id);
        var editId = detail.GetProperty("pendingEdit").GetProperty("id").GetInt64();

        var reject = await Post(buyer.Client, $"/api/v1/deals/{id}/edits/{editId}/reject");
        reject.StatusCode.Should().Be(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());

        var after = await Detail(buyer, id);
        after.GetProperty("amount").GetDecimal().Should().Be(1000m);
        after.GetProperty("pendingEdit").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Accept_is_blocked_while_edit_is_pending_and_edit_is_blocked_after_acceptance()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);

        (await Post(seller.Client, $"/api/v1/deals/{id}/edits", new { amount = 900m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var accept = await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true });
        accept.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var detail = await Detail(buyer, id);
        var editId = detail.GetProperty("pendingEdit").GetProperty("id").GetInt64();
        (await Post(buyer.Client, $"/api/v1/deals/{id}/edits/{editId}/reject"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await Post(seller.Client, $"/api/v1/deals/{id}/accept", new { acceptTerms = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var lateEdit = await Post(seller.Client, $"/api/v1/deals/{id}/edits", new { amount = 900m });
        lateEdit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCode(lateEdit)).Should().Be("DEAL_INVALID_STATE");
    }
}
