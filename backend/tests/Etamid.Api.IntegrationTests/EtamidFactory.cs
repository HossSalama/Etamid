using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Etamid.Application.Common.Interfaces;

namespace Etamid.Api.IntegrationTests;

/// <summary>Runs the real pipeline with an in-memory database and the fake phone verifier.</summary>
public sealed class EtamidFactory : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "integration-tests-webhook-secret-0123456789";

    /// <summary>Controllable clock (offset from real time); zero offset behaves exactly like the system clock.</summary>
    public TestClock Clock { get; } = new();

    public EtamidFactory()
    {
        // Environment variables are read before Program.cs evaluates configuration, so they are reliable here.
        Environment.SetEnvironmentVariable("Persistence__UseInMemory", "true");
        Environment.SetEnvironmentVariable("Persistence__InMemoryName", "etamid-" + Guid.NewGuid());
        Environment.SetEnvironmentVariable("Auth__PhoneVerifier", "Fake");
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-tests-secret-0123456789-abcdef");
        Environment.SetEnvironmentVariable("RateLimiting__AuthPerMinute", "10000");
        Environment.SetEnvironmentVariable("Security__DataKey", "ZXRhbWlkLWRldi1kYXRhLWtleS0zMi1ieXRlcy14eCE=");
        Environment.SetEnvironmentVariable("Storage__LocalPath", Path.Combine(Path.GetTempPath(), "etamid-tests-" + Guid.NewGuid().ToString("N")));
        Environment.SetEnvironmentVariable("Storage__PublicBaseUrl", "http://localhost/files");
        Environment.SetEnvironmentVariable("Nafath__FakeApproveAfterSeconds", "0");
        Environment.SetEnvironmentVariable("Payments__WebhookSecret", WebhookSecret);
        // The background sweep would race the tests that move the clock: they call the same handlers explicitly.
        Environment.SetEnvironmentVariable("Escrow__MaintenanceEnabled", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(IClock)).ToList()) services.Remove(d);
            services.AddSingleton<IClock>(Clock);
        });
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PhoneVerifier"] = "Fake",
                ["Persistence:UseInMemory"] = "true",
            });
        });
    }
}

public sealed class TestClock : IClock
{
    private long _offsetTicks;

    public DateTime UtcNow => DateTime.UtcNow.AddTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
