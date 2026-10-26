using System.Text.Json.Serialization;

namespace Etamid.Application.Common.Models;

/// <summary>
/// The standard response envelope: { success, message, data } plus
/// "errors" only on failures and "meta" only on paginated lists.
/// </summary>
public sealed class ApiResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public object? Data { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ApiError>? Errors { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PageMeta? Meta { get; init; }

    public static ApiResponse Ok(object? data, string message, PageMeta? meta = null)
        => new() { Success = true, Message = message, Data = data, Meta = meta };

    public static ApiResponse Fail(string message, IReadOnlyList<ApiError> errors)
        => new() { Success = false, Message = message, Data = null, Errors = errors };
}

public sealed class ApiError
{
    public required string Code { get; init; }
    public required string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; }

    /// <summary>Extra machine-readable details, e.g. retryAfterSeconds. Flattened into the error object.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? Extras { get; init; }
}

public sealed record PageMeta(int Page, int PageSize, int Total, int TotalPages);
