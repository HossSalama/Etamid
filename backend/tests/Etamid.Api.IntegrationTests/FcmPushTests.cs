using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Notifications;
using Etamid.Infrastructure.Push;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Api.IntegrationTests;

/// <summary>
/// Push (Layer 2, FCM). All tests run without real Firebase: the sender
/// talks to a fake <see cref="IFcmClient"/> (or to the no-op sender when
/// FCM is disabled, which is the default).
/// </summary>
public class FcmPushDisabledTests(EtamidFactory factory) : IClassFixture<EtamidFactory>, IDisposable
{
    private const string Password = "Pass#123";
    private static int _counter = 900;

    private readonly List<IServiceScope> _scopes = [];

    private readonly HttpClient _anon = factory.CreateClient();

    private sealed record Party(HttpClient Client, string Phone, long UserId);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05099{n:D5}";
        var email = $"fcm.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Fcm User " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var data = (await Json(verify)).GetProperty("data");
        var token = data.GetProperty("accessToken").GetString()!;
        var userId = data.GetProperty("user").GetProperty("id").GetInt64();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local, userId);
    }

    private static async Task<long> CreateDeal(Party buyer, Party seller)
    {
        var r = await buyer.Client.PostAsJsonAsync("/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private IApplicationDbContext Db()
    {
        var scope = factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
    }

    public void Dispose()
    {
        foreach (var scope in _scopes)
            scope.Dispose();
    }

    [Fact]
    public async Task Disabled_by_default_sender_is_noop_and_deal_flow_still_works()
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IPushNotificationSender>()
            .Should().BeOfType<NoOpPushSender>();

        var buyer = await NewUser();
        var seller = await NewUser();
        await CreateDeal(buyer, seller);

        // The in-app notification still exists; no push was attempted, so
        // PushSentAt stays null.
        var db = Db();
        var notification = db.Notifications.Single(n => n.UserId == seller.UserId);
        notification.Type.ToString().Should().Be("DealRequest");
        notification.PushSentAt.Should().BeNull();
    }
}

public class FcmPushTests : IClassFixture<EtamidFactory>, IDisposable
{
    private const string Password = "Pass#123";
    private static int _counter = 950;

    private readonly EtamidFactory _factory;
    private readonly HttpClient _anon;
    private readonly FakeFcmClient _fake;
    private readonly List<IServiceScope> _scopes = [];

    public FcmPushTests(EtamidFactory factory)
    {
        // Set the hook BEFORE the first CreateClient: the host (and its DI)
        // is built lazily on first use, so a later assignment would be ignored.
        // The fixture is shared across tests in this class, so reuse the
        // first fake instead of replacing it per test instance.
        _factory = factory;
        if (factory.FcmFake is FakeFcmClient existing)
        {
            _fake = existing;
        }
        else
        {
            _fake = new FakeFcmClient();
            factory.FcmFake = _fake;
        }
        _anon = _factory.CreateClient();
    }

    private sealed record Party(HttpClient Client, string Phone, long UserId);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    private async Task<Party> NewUser()
    {
        var n = Interlocked.Increment(ref _counter);
        var local = $"05099{n:D5}";
        var email = $"fcm.{n}@example.com";

        var registration = await _anon.PostAsJsonAsync("/api/v1/auth/register/individual",
            new { username = "Fcm User " + n, phone = local, email, password = Password, acceptTerms = true });
        registration.StatusCode.Should().Be(HttpStatusCode.Created, await registration.Content.ReadAsStringAsync());
        var code = _factory.Emails.CodeFor(email);
        var verify = await _anon.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = local, code });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());
        var data = (await Json(verify)).GetProperty("data");
        var token = data.GetProperty("accessToken").GetString()!;
        var userId = data.GetProperty("user").GetProperty("id").GetInt64();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Party(client, local, userId);
    }

    private static async Task<long> CreateDeal(Party buyer, Party seller)
    {
        var r = await buyer.Client.PostAsJsonAsync("/api/v1/deals", new
        {
            title = "Used phone", sellerPhoneOrCode = seller.Phone, description = "iPhone in good condition",
            amount = 1000m, durationValue = 24, durationUnit = "hour", needShipping = false, acceptTerms = true
        });
        r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await Json(r)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private static async Task RegisterDevice(Party party, string deviceToken)
    {
        var r = await party.Client.PostAsJsonAsync("/api/v1/devices",
            new { deviceToken, platform = "Ios" });
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
    }

    private IApplicationDbContext Db()
    {
        var scope = _factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
    }

    public void Dispose()
    {
        foreach (var scope in _scopes)
            scope.Dispose();
    }

    [Fact]
    public async Task Push_sent_with_arabic_title_and_deep_link_data()
    {
        _fake.Reset();
        _fake.Handler = tokens => tokens.Select(_ => new FcmSendResult(true, false)).ToList();

        var buyer = await NewUser();
        var seller = await NewUser();
        await RegisterDevice(seller, "fcm-valid-token");
        var dealId = await CreateDeal(buyer, seller);

        _fake.Sent.Should().ContainSingle();
        var sent = _fake.Sent[0];
        sent.Data["type"].Should().Be("DealRequest");
        sent.Data["dealId"].Should().Be(dealId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        sent.Title.Should().NotBeNullOrWhiteSpace();
        sent.Title.Should().Be(sent.Data["titleAr"]);
        sent.Data["titleEn"].Should().NotBeNullOrWhiteSpace();

        var db = Db();
        var notification = db.Notifications.Single(n => n.UserId == seller.UserId);
        notification.PushSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Invalid_token_deactivates_the_device()
    {
        _fake.Reset();
        _fake.Handler = tokens => tokens.Select(_ => new FcmSendResult(false, true)).ToList();

        var buyer = await NewUser();
        var seller = await NewUser();
        await RegisterDevice(seller, "fcm-dead-token");
        await CreateDeal(buyer, seller);

        var db = Db();
        db.Devices.Single(d => d.DeviceToken == "fcm-dead-token").IsActive.Should().BeFalse();
        // The in-app notification itself is untouched by the dead token.
        db.Notifications.Single(n => n.UserId == seller.UserId).Should().NotBeNull();
    }

    [Fact]
    public async Task Sender_failure_never_fails_the_triggering_operation()
    {
        _fake.Reset();
        _fake.Handler = _ => throw new InvalidOperationException("FCM is down");

        var buyer = await NewUser();
        var seller = await NewUser();
        await RegisterDevice(seller, "fcm-any-token");
        var dealId = await CreateDeal(buyer, seller);

        // The deal was created and the notification exists, even though
        // every push attempt threw.
        var db = Db();
        db.Deals.Single(d => d.Id == dealId).Should().NotBeNull();
        var notification = db.Notifications.Single(n => n.UserId == seller.UserId);
        notification.PushSentAt.Should().BeNull();
    }

    /// <summary>Scriptable stand-in for FirebaseMessaging. Tests set Handler per test.</summary>
    private sealed class FakeFcmClient : IFcmClient
    {
        public Func<IReadOnlyList<string>, IReadOnlyList<FcmSendResult>>? Handler { get; set; }
        public List<(string Title, string Body, IReadOnlyDictionary<string, string> Data)> Sent { get; } = [];

        public void Reset()
        {
            Handler = null;
            Sent.Clear();
        }

        public Task<IReadOnlyList<FcmSendResult>> SendMulticastAsync(
            IReadOnlyList<string> tokens, string title, string body,
            IReadOnlyDictionary<string, string> data, CancellationToken ct)
        {
            Sent.Add((title, body, data));
            if (Handler is not null)
                return Task.FromResult(Handler(tokens));
            return Task.FromResult<IReadOnlyList<FcmSendResult>>(
                tokens.Select(_ => new FcmSendResult(true, false)).ToList());
        }
    }
}
