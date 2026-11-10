using Etamid.Application.Common.Interfaces;
using Etamid.Infrastructure.Persistence;
using Etamid.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;


namespace Etamid.Api.IntegrationTests;

/// <summary>Runs the real pipeline with an in-memory database and deterministic external-service fakes.</summary>
public sealed class EtamidFactory : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "integration-tests-webhook-secret-0123456789";

    public TestClock Clock { get; } = new();
    public CapturingEmailService Emails { get; } = new();

    private readonly string _inMemoryName = "etamid-" + Guid.NewGuid();

    public EtamidFactory()
    {
        Environment.SetEnvironmentVariable("Persistence__UseInMemory", "true");
        Environment.SetEnvironmentVariable("Auth__PhoneVerifier", "Fake");
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-tests-secret-0123456789-abcdef");
        Environment.SetEnvironmentVariable("RateLimiting__AuthPerMinute", "10000");
        Environment.SetEnvironmentVariable("RateLimiting__AdminAuthPerMinute", "10000");
        Environment.SetEnvironmentVariable("AdminJwt__Secret", "integration-tests-ADMIN-secret-0123456789-abcdef");
        Environment.SetEnvironmentVariable("Security__DataKey", "ZXRhbWlkLWRldi1kYXRhLWtleS0zMi1ieXRlcy14eCE=");
        Environment.SetEnvironmentVariable("Storage__LocalPath", Path.Combine(Path.GetTempPath(), "etamid-tests-" + Guid.NewGuid().ToString("N")));
        Environment.SetEnvironmentVariable("Storage__PublicBaseUrl", "http://localhost/files");
        Environment.SetEnvironmentVariable("Nafath__FakeApproveAfterSeconds", "0");
        Environment.SetEnvironmentVariable("Payments__WebhookSecret", WebhookSecret);
        Environment.SetEnvironmentVariable("Escrow__MaintenanceEnabled", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(IClock)).ToList()) services.Remove(d);
            services.AddSingleton<IClock>(Clock);

            foreach (var d in services.Where(d => d.ServiceType == typeof(IEmailService)).ToList()) services.Remove(d);
            services.AddSingleton<IEmailService>(Emails);

            // Each factory gets its own in-memory database. The Persistence:InMemoryName
            // setting in ConfigureAppConfiguration is read while Program.cs registers
            // services, before that override is applied, so on its own it cannot
            // isolate the databases. This replaces the DbContext options directly.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(_inMemoryName);
                options.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
            });
        });
        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PhoneVerifier"] = "Fake",
                ["Persistence:UseInMemory"] = "true",
                ["Persistence:InMemoryName"] = _inMemoryName,
            });
        });
    }
}

public sealed class CapturingEmailService : IEmailService
{
    private readonly ConcurrentDictionary<string, string> _codes = new(StringComparer.OrdinalIgnoreCase);
    private int _sentCount;

    public int SentCount => Volatile.Read(ref _sentCount);

    public Task SendOtpEmailAsync(
        string toEmail, string recipientName, string otpCode, OtpEmailKind kind,
        CancellationToken cancellationToken = default)
    {
        _codes[toEmail.Trim()] = otpCode;
        Interlocked.Increment(ref _sentCount);
        return Task.CompletedTask;
    }

    public string CodeFor(string email)
        => _codes.TryGetValue(email.Trim(), out var code)
            ? code
            : throw new InvalidOperationException($"No OTP email was captured for {email}.");
}

public sealed class TestClock : IClock
{
    private long _offsetTicks;

    public DateTime UtcNow => DateTime.UtcNow.AddTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by) => Interlocked.Add(ref _offsetTicks, by.Ticks);

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
