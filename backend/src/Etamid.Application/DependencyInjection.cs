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
            // Innermost: runs closest to the handler, after validation passed,
            // so a push is only ever sent for a request the handler accepted.
            cfg.AddOpenBehavior(typeof(PushNotificationBehavior<,>));
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

        // Disputes: decisions and accepted settlements execute through the escrow ledger
        services.AddScoped<Etamid.Application.Disputes.DisputeExecutionService>();

        // Company deals and installment escrow
        services.AddScoped<Etamid.Application.CompanyDeals.CompanyEscrowService>();

        // Files
        services.AddScoped<Etamid.Application.Files.FileUploadService>();

        // Notifications (Layer 1, in-app only)
        services.AddScoped<Etamid.Application.Notifications.NotificationService>();

        // Push (Layer 2): the no-op is the default. The infrastructure layer
        // replaces this registration with FcmPushSender when FCM is enabled.
        services.AddSingleton<Etamid.Application.Notifications.NoOpPushSender>();
        services.AddScoped<Etamid.Application.Notifications.IPushNotificationSender>(
            sp => sp.GetRequiredService<Etamid.Application.Notifications.NoOpPushSender>());

        return services;
    }
}
