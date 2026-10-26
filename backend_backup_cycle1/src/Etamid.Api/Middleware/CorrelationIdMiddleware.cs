using Serilog.Context;

namespace Etamid.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers.TryGetValue(HeaderName, out var supplied) && !string.IsNullOrWhiteSpace(supplied)
            ? supplied.ToString()
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = id;
        context.Response.Headers[HeaderName] = id;

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await next(context);
        }
    }
}
