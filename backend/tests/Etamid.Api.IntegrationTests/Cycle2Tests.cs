using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

public class Cycle2Tests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private const string IbanRajhi = "SA0380000000608010167519";
    private const string IbanSnb = "SA4310123456789012345678";
    private static int _counter = 100;

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] PdfBytes = "%PDF-1.4 test"u8.ToArray();

    private readonly HttpClient _anon = factory.CreateClient();

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private static string Code(JsonElement body) => body.GetProperty("errors")[0].GetProperty("code").GetString()!;

    /// <summary>Registers + verifies a fresh user and returns a client carrying its access token.</summary>
    private async Task<HttpClient> SignedInClient()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05022{n:D5}";
        var email = $"c2.{n}@example.com";

        await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "User " + n, phone = local, email, password = Password, acceptTerms = true });
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code = factory.Emails.CodeFor(email) });
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string? purpose = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (purpose is not null) form.Add(new StringContent(purpose), "purpose");
        return form;
    }

    // ---------- Config / lookups / FAQ (public)
    [Fact]
    public async Task Config_returns_fee_and_versions()
    {
        var data = (await Json(await _anon.GetAsync("/api/v1/config"))).GetProperty("data");
        data.GetProperty("currency").GetString().Should().Be("SAR");
        data.GetProperty("platformFee").GetProperty("amount").GetDecimal().Should().Be(50);
        data.GetProperty("platformFee").GetProperty("paidBy").GetString().Should().Be("seller");
        data.GetProperty("minVersion").GetProperty("android").GetString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("reject-reasons", 6)]
    [InlineData("dispute-reasons", 6)]
    [InlineData("company-reject-reasons", 5)]
    [InlineData("duration-units", 2)]
    [InlineData("payment-methods", 1)]
    [InlineData("store-categories", 5)]
    [InlineData("banks", 10)]
    [InlineData("shipping-companies", 1)]
    public async Task Lookups_return_seeded_items(string type, int count)
    {
        var response = await _anon.GetAsync($"/api/v1/lookups/{type}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(response)).GetProperty("data").GetArrayLength().Should().Be(count);
    }

    [Fact]
    public async Task Lookups_are_localized()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lookups/payment-methods");
        req.Headers.Add("Accept-Language", "ar");
        var item = (await Json(await _anon.SendAsync(req))).GetProperty("data")[0];
        item.GetProperty("name").GetString().Should().Be("تحويل بنكي");
        item.GetProperty("code").GetString().Should().Be("bank_transfer");
    }

    [Fact]
    public async Task Unknown_lookup_type_is_404()
    {
        var r = await _anon.GetAsync("/api/v1/lookups/nope");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Code(await Json(r)).Should().Be("LOOKUP_TYPE_NOT_FOUND");
    }

    [Fact]
    public async Task Faqs_are_public()
    {
        var r = await _anon.GetAsync("/api/v1/support/faqs");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(r)).GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
    }

    // ---------- Uploads
    [Fact]
    public async Task Upload_requires_authentication()
    {
        var r = await _anon.PostAsync("/api/v1/uploads", FileContent(PngBytes, "a.png"));
        r.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_png_returns_id_and_a_downloadable_url()
    {
        var client = await SignedInClient();
        var r = await client.PostAsync("/api/v1/uploads", FileContent(PngBytes, "a.png", "dispute"));
        r.StatusCode.Should().Be(HttpStatusCode.Created);

        var data = (await Json(r)).GetProperty("data");
        data.GetProperty("fileId").GetString().Should().NotBeNullOrEmpty();
        var url = new Uri(data.GetProperty("url").GetString()!);
        url.AbsolutePath.Should().EndWith(".png");

        var download = await _anon.GetAsync(url.AbsolutePath);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Upload_pdf_is_allowed_but_not_for_a_banner()
    {
        var client = await SignedInClient();
        (await client.PostAsync("/api/v1/uploads", FileContent(PdfBytes, "c.pdf"))).StatusCode.Should().Be(HttpStatusCode.Created);

        var banner = await client.PostAsync("/api/v1/uploads", FileContent(PdfBytes, "c.pdf", "store_banner"));
        banner.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Code(await Json(banner)).Should().Be("FILE_TYPE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Upload_rejects_a_fake_extension()
    {
        var client = await SignedInClient();
        var r = await client.PostAsync("/api/v1/uploads", FileContent("<script>alert(1)</script>"u8.ToArray(), "evil.png"));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Code(await Json(r)).Should().Be("FILE_TYPE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Upload_rejects_files_over_10mb()
    {
        var client = await SignedInClient();
        var big = new byte[10 * 1024 * 1024 + 10];
        PngBytes.CopyTo(big, 0);
        var r = await client.PostAsync("/api/v1/uploads", FileContent(big, "big.png"));
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Code(await Json(r)).Should().Be("FILE_TOO_LARGE");
    }

    [Fact]
    public async Task Avatar_appears_on_the_profile()
    {
        var client = await SignedInClient();
        (await client.PostAsync("/api/v1/me/avatar", FileContent(PngBytes, "me.png"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var me = (await Json(await client.GetAsync("/api/v1/me"))).GetProperty("data");
        me.GetProperty("avatarUrl").GetString().Should().Contain("/avatars/");
    }

    // ---------- Bank account
    [Fact]
    public async Task Bank_account_is_null_until_saved_then_masked()
    {
        var client = await SignedInClient();
        (await Json(await client.GetAsync("/api/v1/me/bank-account"))).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);

        var save = await client.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "Ahmed Ali", iban = IbanRajhi });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = (await Json(await client.GetAsync("/api/v1/me/bank-account"))).GetProperty("data");
        data.GetProperty("holderName").GetString().Should().Be("Ahmed Ali");
        data.GetProperty("ibanMasked").GetString().Should().EndWith("7519").And.NotContain("8000");
        data.GetProperty("bankName").GetString().Should().Be("Al Rajhi Bank");

        // The profile carries the same masked account.
        var me = (await Json(await client.GetAsync("/api/v1/me"))).GetProperty("data");
        me.GetProperty("bankAccount").GetProperty("holderName").GetString().Should().Be("Ahmed Ali");
    }

    [Fact]
    public async Task Invalid_iban_is_422_and_unknown_bank_is_422()
    {
        var client = await SignedInClient();

        var bad = await client.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "A", iban = "SA0380000000608010167518" });
        bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Code(await Json(bad)).Should().Be("INVALID_IBAN");
    }

    [Fact]
    public async Task Same_iban_on_two_users_is_409()
    {
        var first = await SignedInClient();
        var second = await SignedInClient();

        (await first.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "A", iban = IbanSnb })).StatusCode.Should().Be(HttpStatusCode.OK);
        var dup = await second.PutAsJsonAsync("/api/v1/me/bank-account", new { holderName = "B", iban = IbanSnb });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Code(await Json(dup)).Should().Be("IBAN_EXISTS");
    }

    // ---------- Nafath
    [Fact]
    public async Task Nafath_flow_marks_the_user_verified()
    {
        var client = await SignedInClient();

        var start = await client.PostAsync("/api/v1/me/kyc/nafath/start", null);
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await Json(start)).GetProperty("data");
        var requestId = data.GetProperty("requestId").GetString()!;
        data.GetProperty("randomNumber").GetString().Should().HaveLength(2);

        var status = await Json(await client.GetAsync($"/api/v1/me/kyc/nafath/status?requestId={requestId}"));
        status.GetProperty("data").GetProperty("status").GetString().Should().Be("verified");

        var me = (await Json(await client.GetAsync("/api/v1/me"))).GetProperty("data");
        me.GetProperty("nafathVerified").GetBoolean().Should().BeTrue();

        var again = await client.PostAsync("/api/v1/me/kyc/nafath/start", null);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Code(await Json(again)).Should().Be("NAFATH_ALREADY_VERIFIED");
    }

    [Fact]
    public async Task Nafath_status_of_someone_elses_request_is_404()
    {
        var owner = await SignedInClient();
        var other = await SignedInClient();
        var requestId = (await Json(await owner.PostAsync("/api/v1/me/kyc/nafath/start", null)))
            .GetProperty("data").GetProperty("requestId").GetString()!;

        (await other.GetAsync($"/api/v1/me/kyc/nafath/status?requestId={requestId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- Store
    [Fact]
    public async Task Store_is_created_with_category_and_banner()
    {
        var client = await SignedInClient();
        (await Json(await client.GetAsync("/api/v1/me/store"))).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);

        var upload = await client.PostAsync("/api/v1/uploads", FileContent(PngBytes, "banner.png", "store_banner"));
        var fileId = (await Json(upload)).GetProperty("data").GetProperty("fileId").GetString();

        var save = await client.PutAsJsonAsync("/api/v1/me/store", new { storeName = "My Store", categoryId = 21, bannerFileId = fileId });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var store = (await Json(save)).GetProperty("data");
        store.GetProperty("name").GetString().Should().Be("My Store");
        store.GetProperty("category").GetString().Should().Be("Electronics");
        store.GetProperty("bannerUrl").GetString().Should().Contain("/banners/");
    }

    [Fact]
    public async Task Store_rejects_an_unknown_category_and_a_foreign_banner()
    {
        var client = await SignedInClient();
        var other = await SignedInClient();

        var badCategory = await client.PutAsJsonAsync("/api/v1/me/store", new { storeName = "S", categoryId = 9999 });
        badCategory.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var theirs = await other.PostAsync("/api/v1/uploads", FileContent(PngBytes, "b.png", "store_banner"));
        var fileId = (await Json(theirs)).GetProperty("data").GetProperty("fileId").GetString();
        var foreign = await client.PutAsJsonAsync("/api/v1/me/store", new { storeName = "S", bannerFileId = fileId });
        foreign.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- Addresses
    private static object Address(string city = "Riyadh", string phone = "0501234567")
        => new { label = "Home", recipientName = "Ahmed", recipientPhone = phone, city, district = "Olaya", street = "King Fahd Rd", buildingNo = "12", postalCode = "12211" };

    [Fact]
    public async Task Address_crud_with_default_handling()
    {
        var client = await SignedInClient();

        var first = await client.PostAsJsonAsync("/api/v1/me/addresses", Address());
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstData = (await Json(first)).GetProperty("data");
        firstData.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        firstData.GetProperty("recipientPhone").GetString().Should().Be("0501234567");
        var firstId = firstData.GetProperty("id").GetInt64();

        var second = await client.PostAsJsonAsync("/api/v1/me/addresses", Address("Jeddah"));
        var secondId = (await Json(second)).GetProperty("data").GetProperty("id").GetInt64();
        (await Json(second)).GetProperty("data").GetProperty("isDefault").GetBoolean().Should().BeFalse();

        (await client.PutAsync($"/api/v1/me/addresses/{secondId}/default", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await client.PutAsJsonAsync($"/api/v1/me/addresses/{firstId}", Address("Dammam"));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(update)).GetProperty("data").GetProperty("city").GetString().Should().Be("Dammam");

        var list = (await Json(await client.GetAsync("/api/v1/me/addresses"))).GetProperty("data");
        list.GetArrayLength().Should().Be(2);
        list[0].GetProperty("id").GetInt64().Should().Be(secondId);   // default first

        (await client.DeleteAsync($"/api/v1/me/addresses/{secondId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await Json(await client.GetAsync("/api/v1/me/addresses"))).GetProperty("data");
        after.GetArrayLength().Should().Be(1);
        after[0].GetProperty("isDefault").GetBoolean().Should().BeTrue();   // promoted
    }

    [Fact]
    public async Task Address_validation_and_ownership()
    {
        var client = await SignedInClient();
        var other = await SignedInClient();

        var bad = await client.PostAsJsonAsync("/api/v1/me/addresses", Address(phone: "01012345678"));
        bad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Json(bad)).GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("recipientPhone");

        var created = await client.PostAsJsonAsync("/api/v1/me/addresses", Address());
        var id = (await Json(created)).GetProperty("data").GetProperty("id").GetInt64();

        (await other.DeleteAsync($"/api/v1/me/addresses/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PutAsJsonAsync($"/api/v1/me/addresses/{id}", Address())).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Address_limit_is_ten()
    {
        var client = await SignedInClient();
        for (var i = 0; i < 10; i++)
            (await client.PostAsJsonAsync("/api/v1/me/addresses", Address())).StatusCode.Should().Be(HttpStatusCode.Created);

        var eleventh = await client.PostAsJsonAsync("/api/v1/me/addresses", Address());
        eleventh.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Code(await Json(eleventh)).Should().Be("ADDRESS_LIMIT_REACHED");
    }
}
