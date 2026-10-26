using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Etamid.Api.Localization;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Infrastructure.Identity;
using Etamid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Etamid.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMessageLocalizer, JsonMessageLocalizer>();

        services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)))
            .ConfigureApiBehaviorOptions(o =>
            {
                // Model-binding errors (malformed JSON, wrong types) use the same envelope as every other error.
                o.InvalidModelStateResponseFactory = ctx =>
                {
                    var localizer = ctx.HttpContext.RequestServices.GetRequiredService<IMessageLocalizer>();
                    var errors = ctx.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .Select(e => new ApiError
                        {
                            Code = "INVALID_VALUE",
                            Field = ToCamel(e.Key),
                            Message = localizer.Get("INVALID_VALUE")
                        })
                        .ToList();

                    return new Microsoft.AspNetCore.Mvc.ObjectResult(ApiResponse.Fail(localizer.Get("VALIDATION_FAILED"), errors))
                    {
                        StatusCode = StatusCodes.Status422UnprocessableEntity
                    };
                };
            });

        services.Configure<RequestLocalizationOptions>(o =>
        {
            o.SetDefaultCulture("en");
            o.AddSupportedCultures("en", "ar");
            o.AddSupportedUICultures("en", "ar");
        });

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        AddAuthentication(services);
        AddRateLimiting(services, configuration);
        AddSwagger(services);

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    private static void AddAuthentication(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();

        // Configured from DI so the same JwtOptions drive both token creation and validation.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var o = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = o.Issuer,
                    ValidateAudience = true,
                    ValidAudience = o.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Secret))
                };
            });
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerMinute = configuration.GetValue<int?>("RateLimiting:AuthPerMinute") ?? 20;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.OnRejected = async (context, ct) =>
            {
                var localizer = context.HttpContext.RequestServices.GetRequiredService<IMessageLocalizer>();
                var message = localizer.Get("RATE_LIMITED");
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse.Fail(message, [new ApiError { Code = "RATE_LIMITED", Message = message }]), ct);
            };
        });
    }

    private static void AddSwagger(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "Etamid API", Version = "v1" });

            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token only (no 'Bearer ' prefix)."
            });

            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            });
        });
    }

    private static string ToCamel(string key)
        => string.IsNullOrEmpty(key) ? key : char.ToLowerInvariant(key[0]) + key[1..];
}

public sealed class DatabaseHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the database.");
}
