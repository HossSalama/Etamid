using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Etamid.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Seller products: a seller manages their own catalog (add / update / delete),
/// the store page lists it for any logged-in user, and the Excel import is
/// all-or-nothing — one bad row rejects the whole file and saves nothing.
/// </summary>
public class ProductTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 7000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05077{n:D5}";
        var email = $"product.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Product User " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local);
    }

    private Task<long> UserIdOf(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync();
    }

    private static async Task<string> UploadProductImage(Party seller)
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(png), "file", "product.png");
        form.Add(new StringContent("product_image"), "purpose");
        var upload = await seller.Client.PostAsync("/api/v1/uploads", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        return (await Json(upload)).GetProperty("data").GetProperty("fileId").GetString()!;
    }

    private static byte[] Workbook(params (string? Name, string? Description, object? Price, object? Quantity)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("المنتجات");
        sheet.Cell(1, 1).Value = "الاسم";
        sheet.Cell(1, 2).Value = "الوصف";
        sheet.Cell(1, 3).Value = "السعر";
        sheet.Cell(1, 4).Value = "الكمية";
        for (var i = 0; i < rows.Length; i++)
        {
            SetCell(sheet.Cell(i + 2, 1), rows[i].Name);
            SetCell(sheet.Cell(i + 2, 2), rows[i].Description);
            SetCell(sheet.Cell(i + 2, 3), rows[i].Price);
            SetCell(sheet.Cell(i + 2, 4), rows[i].Quantity);
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: break;
            case int i: cell.Value = i; break;
            case decimal d: cell.Value = d; break;
            case string s: cell.Value = s; break;
            default: cell.Value = value.ToString(); break;
        }
    }

    private static async Task<HttpResponseMessage> Import(Party seller, byte[] xlsx)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(xlsx), "file", "products.xlsx");
        return await seller.Client.PostAsync("/api/v1/me/products/import", form);
    }

    [Fact]
    public async Task Add_and_list_products()
    {
        var seller = await NewUser();
        var sellerId = await UserIdOf(seller.Phone);

        var add = await seller.Client.PostAsJsonAsync("/api/v1/me/products", new
        {
            name = "سماعات لاسلكية", description = "بحالة ممتازة", price = 250m, quantity = 3, imagePublicId = (string?)null
        });
        add.StatusCode.Should().Be(HttpStatusCode.Created, await add.Content.ReadAsStringAsync());
        var created = (await Json(add)).GetProperty("data");
        created.GetProperty("name").GetString().Should().Be("سماعات لاسلكية");
        created.GetProperty("price").GetDecimal().Should().Be(250m);
        created.GetProperty("quantity").GetInt32().Should().Be(3);

        var page = await Json(await seller.Client.GetAsync($"/api/v1/sellers/{sellerId}/products"));
        page.GetProperty("data").GetArrayLength().Should().Be(1);
        page.GetProperty("data")[0].GetProperty("name").GetString().Should().Be("سماعات لاسلكية");

        var mine = await Json(await seller.Client.GetAsync("/api/v1/me/products"));
        mine.GetProperty("data").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Product_with_image_shows_image_url()
    {
        var seller = await NewUser();
        var fileId = await UploadProductImage(seller);

        var add = await seller.Client.PostAsJsonAsync("/api/v1/me/products", new
        {
            name = "هاتف", description = (string?)null, price = 900m, quantity = 1, imagePublicId = fileId
        });
        add.StatusCode.Should().Be(HttpStatusCode.Created, await add.Content.ReadAsStringAsync());
        (await Json(add)).GetProperty("data").GetProperty("imageUrl").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Update_delete_and_ownership()
    {
        var seller = await NewUser();
        var stranger = await NewUser();

        var add = await seller.Client.PostAsJsonAsync("/api/v1/me/products", new
        {
            name = "قديم", description = (string?)null, price = 100m, quantity = 2, imagePublicId = (string?)null
        });
        var id = (await Json(add)).GetProperty("data").GetProperty("id").GetInt64();

        var update = await seller.Client.PutAsJsonAsync($"/api/v1/me/products/{id}", new
        {
            name = "جديد", description = "محدّث", price = 150m, quantity = 0, imagePublicId = (string?)null
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        (await Json(update)).GetProperty("data").GetProperty("price").GetDecimal().Should().Be(150m);

        (await stranger.Client.PutAsJsonAsync($"/api/v1/me/products/{id}", new
        {
            name = "x", description = (string?)null, price = 1m, quantity = 1, imagePublicId = (string?)null
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await stranger.Client.DeleteAsync($"/api/v1/me/products/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await seller.Client.DeleteAsync($"/api/v1/me/products/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var mine = await Json(await seller.Client.GetAsync("/api/v1/me/products"));
        mine.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Seller_products_unknown_seller_and_anonymous()
    {
        var seller = await NewUser();
        (await seller.Client.GetAsync("/api/v1/sellers/999999/products")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _anon.GetAsync("/api/v1/sellers/1/products")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Import_good_file_then_rejected_file_saves_nothing()
    {
        var seller = await NewUser();

        var good = await Import(seller, Workbook(
            ("سماعات", "بلوتوث", 250, 3),
            ("شاحن", null, 50, 10)));
        good.StatusCode.Should().Be(HttpStatusCode.OK, await good.Content.ReadAsStringAsync());
        (await Json(good)).GetProperty("data").GetProperty("imported").GetInt32().Should().Be(2);

        var bad = await Import(seller, Workbook(("تالف", null, -5, 1)));
        bad.StatusCode.Should().Be(HttpStatusCode.OK, await bad.Content.ReadAsStringAsync());
        var badJson = await Json(bad);
        badJson.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be("PRODUCT_IMPORT_FAILED");
        // ApiError.Extras is [JsonExtensionData], so the row errors are flattened
        // into the error object itself, not nested under an "extras" key.
        var rowErrors = badJson.GetProperty("errors")[0].GetProperty("errors");
        rowErrors.GetArrayLength().Should().Be(1);
        rowErrors[0].GetProperty("row").GetInt32().Should().Be(2);
        rowErrors[0].GetProperty("field").GetString().Should().Be("price");

        var mine = await Json(await seller.Client.GetAsync("/api/v1/me/products"));
        mine.GetProperty("data").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Template_download_is_a_workbook_with_headers()
    {
        var seller = await NewUser();
        var response = await seller.Client.GetAsync("/api/v1/me/products/template");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        workbook.Worksheets.Should().Contain(w => w.Name == "المنتجات");
        var sheet = workbook.Worksheet("المنتجات");
        sheet.Cell(1, 1).GetString().Should().Be("الاسم");
        sheet.Cell(1, 4).GetString().Should().Be("الكمية");
    }
}
