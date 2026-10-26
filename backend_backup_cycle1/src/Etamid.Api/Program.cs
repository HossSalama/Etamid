using Etamid.Api.Extensions;
using Etamid.Api.Middleware;
using Etamid.Application;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Optional, git-ignored file for personal overrides (connection string, Firebase project, secrets).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRequestLocalization();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Bodyless 401/403/404/405 responses also use the standard envelope.
app.UseStatusCodePages(async ctx =>
{
    var http = ctx.HttpContext;
    var localizer = http.RequestServices.GetRequiredService<IMessageLocalizer>();

    var code = http.Response.StatusCode switch
    {
        401 when http.Response.Headers.WWWAuthenticate.ToString().Contains("expired", StringComparison.OrdinalIgnoreCase) => "TOKEN_EXPIRED",
        401 => "UNAUTHORIZED",
        403 => "FORBIDDEN",
        404 => "NOT_FOUND",
        405 => "METHOD_NOT_ALLOWED",
        _ => "HTTP_ERROR"
    };

    var message = localizer.Get(code);
    await http.Response.WriteAsJsonAsync(
        ApiResponse.Fail(message, [new ApiError { Code = code, Message = message }]));
});

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.Run();

public partial class Program;
