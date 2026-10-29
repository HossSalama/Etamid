using System.Reflection;
using Etamid.Application.Common.Behaviors;
using Etamid.Application.Identity.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Etamid.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Report only the first failure per property (REQUIRED before INVALID_*).
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Identity application services
        services.AddScoped<AuthAuditService>();
        services.AddScoped<UserCodeGenerator>();
        services.AddScoped<SessionService>();
        services.AddScoped<UserProfileService>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<EmailOtpService>();

        // Deals and escrow
        services.AddScoped<Etamid.Application.Deals.Services.EscrowLedgerService>();
        services.AddScoped<Etamid.Application.Deals.Services.EscrowReleaseService>();
        services.AddScoped<Etamid.Application.Deals.Services.IdempotencyService>();

        // Files
        services.AddScoped<Etamid.Application.Files.FileUploadService>();

        return services;
    }
}
