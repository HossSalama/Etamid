using Etamid.Domain.Admin;
using Etamid.SharedKernel;

namespace Etamid.Application.AdminAuth;

public static class AdminErrors
{
    // Every login failure (unknown email, wrong password, disabled) returns this same error.
    public static readonly Error InvalidCredentials = Error.Unauthorized("ADMIN_INVALID_CREDENTIALS");

    public static Error AccountLocked(int retryAfterSeconds)
        => Error.TooManyRequests("ADMIN_ACCOUNT_LOCKED", new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfterSeconds });

    public static readonly Error TwoFactorChallengeInvalid = Error.Unauthorized("ADMIN_2FA_CHALLENGE_INVALID");
    public static readonly Error TwoFactorCodeInvalid = Error.Unauthorized("ADMIN_2FA_CODE_INVALID");
    public static readonly Error TokenInvalid = Error.Unauthorized("ADMIN_TOKEN_INVALID");
    public static readonly Error RefreshReused = Error.Unauthorized("ADMIN_REFRESH_REUSED");

    public static readonly Error Forbidden = Error.Forbidden("ADMIN_FORBIDDEN");
    public static readonly Error SuperAdminRequired = Error.Forbidden("ADMIN_SUPER_ADMIN_REQUIRED");

    public static readonly Error NotFound = Error.NotFound("ADMIN_NOT_FOUND");
    public static readonly Error RoleNotFound = Error.NotFound("ADMIN_ROLE_NOT_FOUND");
    public static readonly Error PermissionNotFound = Error.NotFound("ADMIN_PERMISSION_NOT_FOUND", "permissions");

    public static readonly Error EmailExists = Error.Conflict("ADMIN_EMAIL_EXISTS", "email");
    public static readonly Error RoleNameExists = Error.Conflict("ADMIN_ROLE_NAME_EXISTS", "name");
    public static readonly Error RoleProtected = Error.Conflict("ADMIN_ROLE_PROTECTED");
    public static readonly Error RoleInUse = Error.Conflict("ADMIN_ROLE_IN_USE");
    public static readonly Error CannotDisableSelf = Error.Conflict("ADMIN_CANNOT_DISABLE_SELF");
    public static readonly Error LastSuperAdmin = Error.Conflict("ADMIN_LAST_SUPER_ADMIN");

    public static readonly Error TwoFactorAlreadyEnabled = Error.Conflict("ADMIN_2FA_ALREADY_ENABLED");
    public static readonly Error TwoFactorNotEnabled = Error.Conflict("ADMIN_2FA_NOT_ENABLED");
    public static readonly Error TwoFactorSetupRequired = Error.Conflict("ADMIN_2FA_SETUP_REQUIRED");

    public static readonly string SuperAdminRoleName = AdminRoleNames.SuperAdmin;
}
