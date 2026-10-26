using Etamid.Application.Common.Exceptions;
using FluentValidation;
using MediatR;

namespace Etamid.Application.Common.Behaviors;

/// <summary>
/// Runs every FluentValidation validator for the request. Rules must set an explicit error code
/// with .WithErrorCode("REQUIRED") so the API can return stable codes.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var validatorList = validators.ToList();
        if (validatorList.Count == 0)
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => new ValidationFailureInfo(ToCamelCase(f.PropertyName), ResolveCode(f.ErrorCode)))
            .DistinctBy(f => (f.Field, f.Code))
            .ToList();

        if (failures.Count > 0)
            throw new RequestValidationException(failures);

        return await next();
    }

    private static string ResolveCode(string? code)
        => string.IsNullOrWhiteSpace(code) || code.EndsWith("Validator", StringComparison.Ordinal)
            ? "INVALID_VALUE"
            : code;

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
