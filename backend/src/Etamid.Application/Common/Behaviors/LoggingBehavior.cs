using System.Diagnostics;
using Etamid.Application.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Etamid.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            logger.LogInformation("Handled {Request} in {ElapsedMs} ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var details = ex is RequestValidationException rve
                ? string.Join(", ", rve.Failures.Select(f => $"{f.Field}: {f.Code}"))
                : ex.Message;
            logger.LogWarning("Request {Request} failed after {ElapsedMs} ms: {Error} ({Details})", name, sw.ElapsedMilliseconds, ex.GetType().Name, details);
            throw;
        }
    }
}
