using Etamid.Api.Extensions;
using Etamid.Api.Middleware;
using Etamid.Application;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.Infrastructure;
using Etamid.Infrastructure.Storage;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
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

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStart"))
{
    if (app.Environment.IsProduction())
        throw new InvalidOperationException("Automatic database migrations are disabled in Production.");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<Etamid.Infrastructure.Persistence.ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>("Persistence:UseInMemory"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<Etamid.Infrastructure.Persistence.ApplicationDbContext>().Database.EnsureCreated();
}

// Local-only first super-admin (Development only; needs AdminSeed:Email/Password from user-secrets or env).
if (app.Environment.IsDevelopment())
    await Etamid.Infrastructure.Persistence.AdminBootstrapSeeder.RunAsync(app.Services, app.Configuration);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRequestLocalization();
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

var storageRoot = LocalFileStorage.ResolveRoot(builder.Configuration, builder.Environment);
Directory.CreateDirectory(storageRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRoot),
    RequestPath = "/files",
    OnPrepareResponse = ctx => ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
});

// Bodyless 401/403/404/405 responses also use the standard envelope — and,
// like every other API error, they are rewritten to HTTP 200 with the error
// in the body (owner decision 2026-11-09). The code is read from the
// original status before it is overwritten, so TOKEN_EXPIRED is still
// distinguished from a plain UNAUTHORIZED.
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
    http.Response.StatusCode = StatusCodes.Status200OK;
    await http.Response.WriteAsJsonAsync(
        ApiResponse.Fail(message, [new ApiError { Code = code, Message = message }]));
});

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Etamid.Api v1");
        // Second definition: the Postman collection, converted to OpenAPI.
        // Listing an endpoint here does not implement it — unimplemented
        // collection requests still return NOT_FOUND when tried.
        o.SwaggerEndpoint("/collection-openapi.json", "Etamid Collection");
    });
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Serves the converted Postman collection as OpenAPI for Swagger UI.
// The file is copied to the output folder by the csproj (Swagger/collection-openapi.json).
app.MapGet("/collection-openapi.json", () =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "Swagger", "collection-openapi.json");
    return File.Exists(path) ? Results.File(path, "application/json") : Results.NotFound();
});
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.Run();

public partial class Program;
