using Etamid.SharedKernel;

namespace Etamid.Api.Common;

/// <summary>
/// Retained for reference only. Since 2026-11-09 (owner decision) every API
/// error returns HTTP 200 with the error in the envelope, so no controller
/// maps an error type to an HTTP status anymore; the code in the body
/// identifies the failure.
/// </summary>
public static class HttpStatusMapper
{
    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.PaymentRequired => StatusCodes.Status402PaymentRequired,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status400BadRequest
    };
}
