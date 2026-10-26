using Etamid.SharedKernel;

namespace Etamid.Application.Common.Errors;

public static class CommonErrors
{
    public static readonly Error NotFound = Error.NotFound("NOT_FOUND");
    public static readonly Error Unauthorized = Error.Unauthorized("UNAUTHORIZED");
    public static readonly Error Forbidden = Error.Forbidden("FORBIDDEN");
    public static readonly Error Internal = Error.Failure("INTERNAL_ERROR");
}
