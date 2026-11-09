using Etamid.Application.Common.Exceptions;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;

namespace Etamid.Api.Middleware;

/// <summary>Turns unhandled exceptions into the standard envelope. Never leaks internal details.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IMessageLocalizer localizer)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected: nothing to write.
        }
        catch (RequestValidationException ex)
        {
            var errors = ex.Failures
                .Select(f => new ApiError { Code = f.Code, Field = f.Field, Message = localizer.Get(f.Code) })
                .ToList();

            // Like every other API error, validation failures return HTTP 200
            // with the error in the envelope (owner decision 2026-11-09).
            await Write(context, StatusCodes.Status200OK,
                ApiResponse.Fail(localizer.Get("VALIDATION_FAILED"), errors));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            // Unhandled exceptions also return HTTP 200 with the envelope
            // (owner decision 2026-11-09); the detail is only in the log.
            await Write(context, StatusCodes.Status200OK,
                ApiResponse.Fail(localizer.Get("INTERNAL_ERROR"),
                    [new ApiError { Code = "INTERNAL_ERROR", Message = localizer.Get("INTERNAL_ERROR") }]));
        }
    }

    private static async Task Write(HttpContext context, int status, ApiResponse body)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(body);
    }
}
