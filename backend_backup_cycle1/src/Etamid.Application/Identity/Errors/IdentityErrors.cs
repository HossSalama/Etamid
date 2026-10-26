using Etamid.SharedKernel;

namespace Etamid.Application.Identity.Errors;

public static class IdentityErrors
{
    public static readonly Error PhoneExists = Error.Conflict("PHONE_EXISTS", "phone");
    public static readonly Error EmailExists = Error.Conflict("EMAIL_EXISTS", "email");
    public static readonly Error CommercialRegExists = Error.Conflict("COMMERCIAL_REG_EXISTS", "commercialRegNo");
    public static readonly Error InvalidPhone = Error.Validation("INVALID_PHONE", "phone");

    public static readonly Error InvalidCredentials = Error.Unauthorized("INVALID_CREDENTIALS");
    public static readonly Error AccountNotVerified = Error.Forbidden("ACCOUNT_NOT_VERIFIED");
    public static readonly Error AccountSuspended = Error.Forbidden("ACCOUNT_SUSPENDED");

    public static Error AccountLocked(int retryAfterSeconds)
        => Error.TooManyRequests("ACCOUNT_LOCKED", new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfterSeconds });

    public static readonly Error TokenInvalid = Error.Unauthorized("TOKEN_INVALID");
    public static readonly Error RefreshReused = Error.Unauthorized("REFRESH_REUSED");

    /// <summary>Firebase token missing, invalid, expired, for another number, too old, or replayed.</summary>
    public static readonly Error PhoneTokenInvalid = Error.Failure("PHONE_TOKEN_INVALID", "firebaseIdToken");
    public static readonly Error PhoneAlreadyVerified = Error.Conflict("PHONE_ALREADY_VERIFIED", "phone");

    public static readonly Error UserNotFound = Error.NotFound("USER_NOT_FOUND");
    public static readonly Error WrongCurrentPassword = Error.Validation("INVALID_CREDENTIALS", "currentPassword");
    public static readonly Error TermsNotFound = Error.NotFound("TERMS_NOT_FOUND");
    public static readonly Error InvalidTermsType = Error.Validation("INVALID_VALUE", "type");
}
