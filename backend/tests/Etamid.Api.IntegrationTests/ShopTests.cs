using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Product discovery: the shop feed lists every seller's products with the
/// seller and store they belong to, and the seller-lookup preview carries the
/// seller id so the new-deal page can link to that seller's store.
/// </summary>
public class ShopTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 8000;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone, string Name);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05088{n:D5}";
        var email = $"shop.{n}@example.com";
        var name = "Shop User " + n;

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

    private Task<long> UserIdOf(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return db.Users.AsNoTracking().Where(u => u.Phone == phone).Select(u => u.Id).FirstAsync();
    }

    [Fact]
    public async Task Shop_lists_products_with_seller_info()
    {
        var seller = await NewUser();
        var buyer = await NewUser();
        var sellerId = await UserIdOf(seller.Phone);

        var store = await seller.Client.PutAsJsonAsync("/api/v1/me/store", new
        {
            storeName = "متجر الاختبار", categoryId = (int?)null, bannerFileId = (string?)null
        });
        store.StatusCode.Should().Be(HttpStatusCode.OK, await store.Content.ReadAsStringAsync());

        var add = await seller.Client.PostAsJsonAsync("/api/v1/me/products", new
        {
            name = "منتج للعرض", description = (string?)null, price = 75m, quantity = 4, imagePublicId = (string?)null
        });
        add.StatusCode.Should().Be(HttpStatusCode.Created, await add.Content.ReadAsStringAsync());

        var lookup = await Json(await buyer.Client.GetAsync($"/api/v1/users/lookup?q={seller.Phone}"));
        var sellerCode = lookup.GetProperty("data").GetProperty("code").GetString()!;

        var shop = await Json(await buyer.Client.GetAsync("/api/v1/products"));
        var items = shop.GetProperty("data");
        // Found by name, not by count: other tests in this class also add
        // products to the shared feed, so the feed is never exactly one item.
        var item = items.EnumerateArray().First(i => i.GetProperty("name").GetString() == "منتج للعرض");
        item.GetProperty("sellerId").GetInt64().Should().Be(sellerId);
        item.GetProperty("sellerName").GetString().Should().Be(seller.Name);
        item.GetProperty("storeName").GetString().Should().Be("متجر الاختبار");
        item.GetProperty("sellerCode").GetString().Should().Be(sellerCode);
    }

    private static int _companyCounter = 96000;

    private async Task<Party> NewCompany()
    {
        var n = Interlocked.Increment(ref _companyCounter);
        var local = $"05096{n:D5}";
        var email = $"company-shop.{n}@example.com";
        var name = "S Company " + n;

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/company",
            new { companyName = name, ownerPhone = local, commercialRegNo = "CR" + n, taxNo = (string?)null, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var token = (await Json(verify)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local, name);
    }

    [Fact]
    public async Task Shop_marks_company_seller_products()
    {
        var seller = await NewCompany();
        var buyer = await NewUser();

        var add = await seller.Client.PostAsJsonAsync("/api/v1/me/products", new
        {
            name = "منتج شركة", description = (string?)null, price = 1999m, quantity = 3, imagePublicId = (string?)null
        });
        add.StatusCode.Should().Be(HttpStatusCode.Created, await add.Content.ReadAsStringAsync());

        var shop = await Json(await buyer.Client.GetAsync("/api/v1/products"));
        var item = shop.GetProperty("data").EnumerateArray().First(i => i.GetProperty("name").GetString() == "منتج شركة");
        item.GetProperty("isCompany").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Shop_marks_individual_seller_products_not_company()
    {
        var seller = await NewUser();
        var buyer = await NewUser();

        var add = await seller.Client.PostAsJsonAsync("/api/v1/me/products", new
        {
            name = "منتج فرد", description = (string?)null, price = 50m, quantity = 2, imagePublicId = (string?)null
        });
        add.StatusCode.Should().Be(HttpStatusCode.Created, await add.Content.ReadAsStringAsync());

        var shop = await Json(await buyer.Client.GetAsync("/api/v1/products"));
        var item = shop.GetProperty("data").EnumerateArray().First(i => i.GetProperty("name").GetString() == "منتج فرد");
        item.GetProperty("isCompany").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Shop_requires_auth()
    {
        (await _anon.GetAsync("/api/v1/products")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lookup_returns_seller_id_for_store_link()
    {
        var seller = await NewUser();
        var buyer = await NewUser();
        var sellerId = await UserIdOf(seller.Phone);

        var lookup = await Json(await buyer.Client.GetAsync($"/api/v1/users/lookup?q={seller.Phone}"));
        lookup.GetProperty("data").GetProperty("sellerId").GetInt64().Should().Be(sellerId);
    }
}
