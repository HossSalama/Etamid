namespace Etamid.Application.Common.Exceptions;

public sealed record ValidationFailureInfo(string Field, string Code);

/// <summary>Thrown by the validation pipeline behavior; mapped to HTTP 422 by the API.</summary>
public sealed class RequestValidationException(IReadOnlyList<ValidationFailureInfo> failures)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyList<ValidationFailureInfo> Failures { get; } = failures;
}
