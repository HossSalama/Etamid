using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Deal detail: the counterparty's avatar and WhatsApp number are visible to the
/// other party of the deal (and only to them — strangers get the same 404 as a
/// missing deal, so ids cannot be probed).
/// </summary>
public class DealCounterpartyTests(EtamidFactory factory) : IClassFixture<EtamidFactory>
{
    private const string Password = "Pass#123";
    private static int _counter = 950;

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05080{n:D5}";
        var email = $"counterparty.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Counterparty User " + n, phone = local, email, password = Password, acceptTerms = true });
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

    private static string Key() => Guid.NewGuid().ToString("N");

    private async Task<T> Db<T>(Func<IApplicationDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    private async Task<long> CreateDeal(Party buyer, Party seller)
    {
        var r = await Post(seller.Client, "/api/v1/deals", new
        {
            title = "Used phone", buyerPhoneOrCode = buyer.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    // Give the seller of this deal an avatar file, directly in the DB.
    private async Task SetSellerAvatarAsync(long dealId, string storageKey)
    {
        await Db(async db =>
        {
            var sellerId = await db.Deals.AsNoTracking().Where(d => d.Id == dealId).Select(d => d.SellerId).FirstAsync();
            var user = await db.Users.FirstAsync(u => u.Id == sellerId);
            var file = new StoredFile
            {
                PublicId = Guid.NewGuid(), OwnerUserId = sellerId, Purpose = FilePurpose.Avatar,
                StorageKey = storageKey, FileName = "avatar.png", ContentType = "image/png",
                SizeBytes = 123, Status = FileStatus.Uploaded
            };
            db.Files.Add(file);
            await db.SaveChangesAsync();
            user.SetAvatar(file.Id);
            await db.SaveChangesAsync();
            return 0;
        });
    }

    [Fact]
    public async Task Detail_shows_counterparty_avatar_and_whatsapp()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);

        var storageKey = "avatars/seller-" + Key() + ".png";
        await SetSellerAvatarAsync(id, storageKey);

        var detail = (await Json(await buyer.Client.GetAsync($"/api/v1/deals/{id}"))).GetProperty("data");
        detail.GetProperty("counterpartyName").GetString().Should().StartWith("Counterparty User");
        detail.GetProperty("counterpartyAvatarUrl").GetString().Should().Contain(storageKey);
        // E164: the seller's local 05… number with +966 in front.
        detail.GetProperty("counterpartyWhatsapp").GetString().Should().Be("+966" + seller.Phone[1..]);
    }

    [Fact]
    public async Task Detail_without_counterparty_avatar_has_null_avatar_url_but_whatsapp()
    {
        var buyer = await NewUser();
        var seller = await NewUser();
        var id = await CreateDeal(buyer, seller);

        var detail = (await Json(await buyer.Client.GetAsync($"/api/v1/deals/{id}"))).GetProperty("data");
        detail.GetProperty("counterpartyAvatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
        detail.GetProperty("counterpartyWhatsapp").GetString().Should().Be("+966" + seller.Phone[1..]);
    }
}
