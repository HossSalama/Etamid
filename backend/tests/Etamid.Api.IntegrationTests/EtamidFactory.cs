using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Etamid.Api.IntegrationTests;

/// <summary>Runs the real pipeline with an in-memory database and the fake phone verifier.</summary>
public sealed class EtamidFactory : WebApplicationFactory<Program>
{
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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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
