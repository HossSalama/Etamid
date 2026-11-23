using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>Support tickets v1: user create/list/detail/reply/close, contact form, admin
/// assign/reply/close, reply notifications, ticket_subject lookups, FAQ content.</summary>
public class SupportTicketTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 21000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone, string Name);

    // ------------------------------------------------------------------ helpers
    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static async Task<JsonElement> Data(HttpResponseMessage r) => (await Json(r)).GetProperty("data");

    private static async Task<string> Code(HttpResponseMessage r)
        => (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private static async Task<HttpResponseMessage> Post(HttpClient c, string url, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        return await c.SendAsync(request);
    }

    private async Task<Party> NewUser(string name)
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05072{n:D5}";
        var email = $"support.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = name, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local, name);
    }

    private async Task<long> TicketSubjectId(string code)
    {
        var body = await Data(await _anon.GetAsync("/api/v1/lookups/ticket-subjects"));
        foreach (var item in body.GetProperty("subjects").EnumerateArray())
            if (item.GetProperty("code").GetString() == code)
                return item.GetProperty("id").GetInt64();
        throw new InvalidOperationException($"Ticket subject '{code}' not seeded.");
    }

    private async Task<long> CreateTicket(Party who, string message = "The seller never shipped my order.")
    {
        var subjectId = await TicketSubjectId("deal");
        var r = await Post(who.Client, "/api/v1/support/tickets",
            new { subjectId, message });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Data(r)).GetProperty("id").GetInt64();
    }

    // ------------------------------------------------------------------ tests
    [Fact]
    public async Task Unauthenticated_returns_unauthorized_envelope()
    {
        var r = await _anon.GetAsync("/api/v1/support/tickets");

        // Auth challenges carry no body, so the errors-200 status-pages rule
        // rewrites them to HTTP 200 with UNAUTHORIZED in the envelope.
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await Json(r);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Faqs_returns_the_six_design_questions()
    {
        // FAQs are localized: without Accept-Language the API answers in
        // English, so ask for Arabic explicitly like the mobile app does.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/support/faqs");
        request.Headers.Add("Accept-Language", "ar");
        var body = await Data(await _anon.SendAsync(request));

        body.GetArrayLength().Should().Be(6);
        body[0].GetProperty("question").GetString().Should().Be("ما هي منصة اعتمد؟");
        body[1].GetProperty("question").GetString().Should().Be("كيف يمكنني إنشاء حساب جديد؟");
        body[2].GetProperty("question").GetString().Should().Be("كيف يمكنني الدفع في المنصة؟");
        body[3].GetProperty("question").GetString().Should().Be("هل بياناتي الشخصية والمالية آمنة؟");
        body[4].GetProperty("question").GetString().Should().Be("هل توجد رسوم لاستخدام المنصة؟");
        body[5].GetProperty("question").GetString().Should().Be("كيف يمكنني متابعة طلباتي؟");
        foreach (var faq in body.EnumerateArray())
            faq.GetProperty("answer").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Lookups_ticket_subjects_returns_five_with_whatsapp()
    {
        var body = await Data(await _anon.GetAsync("/api/v1/lookups/ticket-subjects"));

        body.GetProperty("supportWhatsapp").GetString().Should().NotBeNull();
        var subjects = body.GetProperty("subjects");
        subjects.GetArrayLength().Should().Be(5);
        subjects.EnumerateArray().Select(i => i.GetProperty("code").GetString()).Should()
            .BeEquivalentTo(["payment", "deal", "account", "technical", "other"]);
    }

    [Fact]
    public async Task Create_ticket_with_subject_id()
    {
        var user = await NewUser("Ticket User");
        var subjectId = await TicketSubjectId("payment");

        var r = await Post(user.Client, "/api/v1/support/tickets",
            new { subjectId, message = "My payment is stuck." });

        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var data = await Data(r);
        data.GetProperty("number").GetString().Should().StartWith("SUP-");
        data.GetProperty("status").GetString().Should().Be("open");
        data.GetProperty("subject").GetString().Should().NotBeNullOrEmpty();
        var messages = data.GetProperty("messages");
        messages.GetArrayLength().Should().Be(1);
        messages[0].GetProperty("senderType").GetString().Should().Be("user");
        messages[0].GetProperty("message").GetString().Should().Be("My payment is stuck.");
    }

    [Fact]
    public async Task Create_ticket_with_free_subject_and_linked_deal()
    {
        var buyer = await NewUser("Ticket Buyer");
        var seller = await NewUser("Ticket Seller");
        var deal = await Post(seller.Client, "/api/v1/deals", new
        {
            title = "Used phone", buyerPhoneOrCode = buyer.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        deal.StatusCode.Should().Be(HttpStatusCode.Created, await deal.Content.ReadAsStringAsync());
        var dealId = (await Data(deal)).GetProperty("id").GetInt64();

        var r = await Post(buyer.Client, "/api/v1/support/tickets",
            new { subject = "مشكلة في الصفقة", message = "Need help with this deal.", dealId });

        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var data = await Data(r);
        data.GetProperty("subject").GetString().Should().Be("مشكلة في الصفقة");
        data.GetProperty("dealId").GetInt64().Should().Be(dealId);
        data.GetProperty("dealTitle").GetString().Should().Be("Used phone");
    }

    [Fact]
    public async Task Create_ticket_rejects_missing_subject_and_message()
    {
        var user = await NewUser("Ticket Validation");

        var noMessage = await Post(user.Client, "/api/v1/support/tickets",
            new { subject = "Hello", message = "" });
        (await Code(noMessage)).Should().Be("INVALID_VALUE");

        var noSubject = await Post(user.Client, "/api/v1/support/tickets",
            new { message = "Hello" });
        (await Code(noSubject)).Should().Be("INVALID_VALUE");

        var badSubjectId = await Post(user.Client, "/api/v1/support/tickets",
            new { subjectId = 999999, message = "Hello" });
        (await Code(badSubjectId)).Should().Be("INVALID_VALUE");
    }

    [Fact]
    public async Task My_tickets_tabs_filter_open_and_closed()
    {
        var user = await NewUser("Ticket Tabs");
        var first = await CreateTicket(user, "First ticket.");
        await CreateTicket(user, "Second ticket.");

        var close = await Post(user.Client, $"/api/v1/support/tickets/{first}/close");
        close.StatusCode.Should().Be(HttpStatusCode.OK, await close.Content.ReadAsStringAsync());

        var open = await Data(await user.Client.GetAsync("/api/v1/support/tickets?status=open"));
        open.GetArrayLength().Should().Be(1);
        var closed = await Data(await user.Client.GetAsync("/api/v1/support/tickets?status=closed"));
        closed.GetArrayLength().Should().Be(1);
        var all = await Data(await user.Client.GetAsync("/api/v1/support/tickets?status=all"));
        all.GetArrayLength().Should().Be(2);

        var bad = await user.Client.GetAsync("/api/v1/support/tickets?status=bogus");
        (await Code(bad)).Should().Be("INVALID_VALUE");
    }

    [Fact]
    public async Task Full_thread_user_admin_replies_and_notification()
    {
        var user = await NewUser("Ticket Thread");
        var admin = await AdminTestKit.CreateAsync(factory, "admin.support.manage");
        var ticketId = await CreateTicket(user);

        // The admin list is global and the test database is shared across this
        // class, so other tests' open tickets are in it too — assert on our
        // own ticket instead of an exact global count.
        var list = await Data(await admin.Client.GetAsync("/api/v1/admin/support/tickets?status=open"));
        var mine = list.EnumerateArray().Single(t => t.GetProperty("id").GetInt64() == ticketId);
        mine.GetProperty("userName").GetString().Should().Be("Ticket Thread");

        var assign = await Post(admin.Client, $"/api/v1/admin/support/tickets/{ticketId}/assign",
            new { adminId = admin.AdminId });
        assign.StatusCode.Should().Be(HttpStatusCode.OK, await assign.Content.ReadAsStringAsync());
        (await Data(assign)).GetProperty("status").GetString().Should().Be("in_review");

        var reply = await Post(admin.Client, $"/api/v1/admin/support/tickets/{ticketId}/messages",
            new { message = "We are looking into it." });
        reply.StatusCode.Should().Be(HttpStatusCode.Created, await reply.Content.ReadAsStringAsync());

        var notifications = await Data(await user.Client.GetAsync("/api/v1/notifications?page=1&pageSize=20"));
        notifications.EnumerateArray().Select(n => n.GetProperty("type").GetString()).Should()
            .Contain("support_ticket");

        var userReply = await Post(user.Client, $"/api/v1/support/tickets/{ticketId}/messages",
            new { message = "Thank you, waiting." });
        userReply.StatusCode.Should().Be(HttpStatusCode.Created, await userReply.Content.ReadAsStringAsync());

        var detail = await Data(await user.Client.GetAsync($"/api/v1/support/tickets/{ticketId}"));
        var messages = detail.GetProperty("messages");
        messages.GetArrayLength().Should().Be(3);
        messages[0].GetProperty("senderType").GetString().Should().Be("user");
        messages[1].GetProperty("senderType").GetString().Should().Be("admin");
        messages[1].GetProperty("senderName").GetString().Should().NotBeNullOrEmpty();
        messages[2].GetProperty("senderType").GetString().Should().Be("user");
        detail.GetProperty("status").GetString().Should().Be("in_review");
    }

    [Fact]
    public async Task User_close_then_reclose_fails()
    {
        var user = await NewUser("Ticket Closer");
        var ticketId = await CreateTicket(user);

        var close = await Post(user.Client, $"/api/v1/support/tickets/{ticketId}/close");
        close.StatusCode.Should().Be(HttpStatusCode.OK, await close.Content.ReadAsStringAsync());
        (await Data(close)).GetProperty("status").GetString().Should().Be("closed");

        var again = await Post(user.Client, $"/api/v1/support/tickets/{ticketId}/close");
        (await Code(again)).Should().Be("SUPPORT_TICKET_INVALID_STATE");
    }

    [Fact]
    public async Task Other_user_cannot_read_ticket()
    {
        var owner = await NewUser("Ticket Owner");
        var stranger = await NewUser("Ticket Stranger");
        var ticketId = await CreateTicket(owner);

        var r = await stranger.Client.GetAsync($"/api/v1/support/tickets/{ticketId}");
        (await Code(r)).Should().Be("SUPPORT_TICKET_NOT_FOUND");

        var reply = await Post(stranger.Client, $"/api/v1/support/tickets/{ticketId}/messages",
            new { message = "Hello" });
        (await Code(reply)).Should().Be("SUPPORT_TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Contact_creates_ticket_with_200_char_limit()
    {
        var user = await NewUser("Contact User");
        var subjectId = await TicketSubjectId("technical");

        var r = await Post(user.Client, "/api/v1/support/contact",
            new { subjectId, message = "The app crashes on login." });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var data = await Data(r);
        data.GetProperty("number").GetString().Should().StartWith("SUP-");

        var mine = await Data(await user.Client.GetAsync("/api/v1/support/tickets?status=all"));
        mine.GetArrayLength().Should().Be(1);

        var tooLong = await Post(user.Client, "/api/v1/support/contact",
            new { subjectId, message = new string('x', 201) });
        (await Code(tooLong)).Should().Be("TOO_LONG");
    }

    [Fact]
    public async Task Admin_without_permission_gets_forbidden()
    {
        var admin = await AdminTestKit.CreateAsync(factory);
        var user = await NewUser("Ticket Forbidden");
        await CreateTicket(user);

        // Auth failures return HTTP 200 with the code in the envelope
        // (errors-200 rule), not a literal 403.
        var list = await admin.Client.GetAsync("/api/v1/admin/support/tickets");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(list)).Should().Be("ADMIN_FORBIDDEN");

        var appUser = await user.Client.GetAsync("/api/v1/admin/support/tickets");
        appUser.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Code(appUser)).Should().Be("ADMIN_FORBIDDEN");
    }
}
