using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Deal image: an optional product photo attached at creation. The upload
/// endpoint already existed (purpose "deal_image"); what was missing was the
/// wiring — the create command ignored images and always stored null.
/// </summary>
public class DealImageTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 900;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05077{n:D5}";
        var email = $"img.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Image User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    // A real 1x1 PNG, so the file sniffer detects an image (not just the extension).
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8Xw8AAwMCAO+ip1sAAAAASUVORK5CYII=");

    private static async Task<string> UploadImage(Party who)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(PngBytes), "file", "product.png");
        content.Add(new StringContent("deal_image"), "purpose");
        var r = await who.Client.PostAsync("/api/v1/uploads", content);
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("fileId").GetString()!;
    }

    private static async Task<JsonElement> CreateDeal(Party buyer, Party seller, string? imagePublicId = null)
    {
        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true,
            imagePublicId
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data");
    }

    [Fact]
    public async Task Deal_created_with_image_returns_its_url_in_detail_and_list()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var fileId = await UploadImage(buyer);

        var created = await CreateDeal(buyer, seller, fileId);
        var id = created.GetProperty("id").GetInt64();
        created.GetProperty("imageUrl").GetString().Should().Contain("deals/");

        var detail = (await Json(await buyer.Client.GetAsync($"/api/v1/deals/{id}"))).GetProperty("data");
        detail.GetProperty("imageUrl").GetString().Should().Contain("deals/");

        var list = (await Json(await buyer.Client.GetAsync("/api/v1/deals?page=1&pageSize=50"))).GetProperty("data");
        var listed = list.EnumerateArray().First(e => e.GetProperty("id").GetInt64() == id);
        listed.GetProperty("imageUrl").GetString().Should().Contain("deals/");
    }

    [Fact]
    public async Task Deal_without_image_has_null_image_url()
    {
        var buyer = await NewUser();
        var seller = await NewUser();

        var created = await CreateDeal(buyer, seller);
        created.GetProperty("imageUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Buyer_cannot_attach_another_users_image()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var foreignFileId = await UploadImage(seller);

        var r = await Post(buyer.Client, "/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true,
            imagePublicId = foreignFileId
        });
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await r.Content.ReadAsStringAsync());
        (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("INVALID_VALUE");
    }

    [Fact]
    public async Task Non_image_upload_as_deal_image_is_rejected()
    {
        var buyer = await NewUser();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("%PDF-1.4 fake"u8.ToArray()), "file", "doc.pdf");
        content.Add(new StringContent("deal_image"), "purpose");
        var r = await buyer.Client.PostAsync("/api/v1/uploads", content);
        r.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await r.Content.ReadAsStringAsync());
        (await Json(r)).GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("FILE_TYPE_NOT_ALLOWED");
    }
}
