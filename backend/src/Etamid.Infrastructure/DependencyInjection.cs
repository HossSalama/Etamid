using Etamid.Application.Common.Interfaces;
using Etamid.Infrastructure.Identity;
using Etamid.Infrastructure.Persistence;
using Etamid.Infrastructure.Persistence.Interceptors;
using Etamid.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Etamid.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IRequestContext, RequestContext>();
        services.AddSingleton<IFileUrlResolver, FileUrlResolver>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<IOtpService, InMemoryOtpService>();

        AddPersistence(services, configuration, environment);
        AddIdentity(services, configuration, environment);
        AddPlatformServices(services, configuration, environment);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var useInMemory = configuration.GetValue<bool>("Persistence:UseInMemory");
        if (useInMemory && environment.IsProduction())
            throw new InvalidOperationException("The in-memory database cannot be used in Production.");

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!useInMemory && string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        var inMemoryName = configuration["Persistence:InMemoryName"] ?? "etamid-tests";

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            if (useInMemory)
                options.UseInMemoryDatabase(inMemoryName);
            else
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

            options.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
    }

    private static void AddIdentity(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .Validate(o => o.Secret.Length >= 32, "Jwt:Secret must be at least 32 characters.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();

        var mode = configuration["Auth:PhoneVerifier"] ?? "Firebase";
        if (mode.Equals("Fake", StringComparison.OrdinalIgnoreCase))
        {
            if (environment.IsProduction())
                throw new InvalidOperationException("The fake phone verifier cannot be used in Production.");

            services.AddSingleton<IPhoneVerifier, FakePhoneVerifier>();
        }
        else
        {
            services.AddOptions<FirebaseOptions>()
                .Bind(configuration.GetSection(FirebaseOptions.Section))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ProjectId), "Firebase:ProjectId is required.")
                .ValidateOnStart();

            services.AddSingleton<IPhoneVerifier, FirebasePhoneVerifier>();
        }
    }

    private static void AddPlatformServices(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<Security.DataProtectionOptions>()
            .Bind(configuration.GetSection(Security.DataProtectionOptions.Section))
            .Validate(o => TryGet32Bytes(o.DataKey), "Security:DataKey must be a base64 string of exactly 32 bytes.")
            .ValidateOnStart();
        services.AddSingleton<IFieldProtector, Security.AesGcmFieldProtector>();

        services.AddSingleton<IFileStorage, Storage.LocalFileStorage>();

        var nafath = configuration["Nafath:Provider"] ?? "Fake";
        if (!nafath.Equals("Fake", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the fake Nafath client exists so far. Set Nafath:Provider to Fake.");
        if (environment.IsProduction())
            throw new InvalidOperationException("The fake Nafath client cannot be used in Production.");

        services.AddSingleton<INafathClient, FakeNafathClient>();

        AddPayments(services, configuration, environment);
    }

    private static void AddPayments(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var provider = configuration["Payments:Provider"] ?? "Fake";
        if (!provider.Equals("Fake", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the fake payment gateway exists so far. Set Payments:Provider to Fake.");
        if (environment.IsProduction())
            throw new InvalidOperationException("The fake payment gateway cannot be used in Production.");

        services.AddSingleton<IPaymentGateway, Payments.FakePaymentGateway>();
        services.AddSingleton<IWebhookSignatureVerifier, Payments.HmacWebhookSignatureVerifier>();

        if (configuration.GetValue("Escrow:MaintenanceEnabled", true))
            services.AddHostedService<Jobs.EscrowMaintenanceWorker>();
    }

    private static bool TryGet32Bytes(string value)
    {
        try { return Convert.FromBase64String(value).Length == 32; }
        catch (FormatException) { return false; }
    }
}
