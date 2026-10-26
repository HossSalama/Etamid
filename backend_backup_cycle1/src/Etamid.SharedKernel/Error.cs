namespace Etamid.SharedKernel;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    PaymentRequired,
    TooManyRequests
}

/// <summary>
/// A stable, machine-readable error. <see cref="Code"/> is part of the public API contract
/// (e.g. OTP_INVALID) and is also the key used to look up the localized message.
/// </summary>
public sealed record Error(
    string Code,
    ErrorType Type,
    string? Field = null,
    IReadOnlyDictionary<string, object?>? Extras = null)
{
    public static readonly Error None = new(string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string? field = null) => new(code, ErrorType.Failure, field);
    public static Error Validation(string code, string? field = null) => new(code, ErrorType.Validation, field);
    public static Error NotFound(string code, string? field = null) => new(code, ErrorType.NotFound, field);
    public static Error Conflict(string code, string? field = null) => new(code, ErrorType.Conflict, field);
    public static Error Unauthorized(string code) => new(code, ErrorType.Unauthorized);
    public static Error Forbidden(string code) => new(code, ErrorType.Forbidden);
    public static Error PaymentRequired(string code) => new(code, ErrorType.PaymentRequired);
    public static Error TooManyRequests(string code, IReadOnlyDictionary<string, object?>? extras = null)
        => new(code, ErrorType.TooManyRequests, null, extras);
}
