namespace Etamid.Domain.Identity;

public enum UserType { Individual, Company }
public enum AccountStatus { PendingVerification, Active, Suspended, Deleted }
public enum Language { Ar, En }
public enum DevicePlatform { Android, Ios }
public enum NafathStatus { Pending, Verified, Rejected, Expired }
public enum TermsType { Platform, Deal, Company }
public enum RefreshRevokeReason { Logout, LogoutAll, Rotated, ReuseDetected, PasswordChanged, PasswordReset }

public enum AuthEvent
{
    Registered,
    PhoneVerified,
    PhoneVerificationFailed,
    LoginSuccess,
    LoginFailed,
    AccountLocked,
    PasswordReset,
    PasswordChanged,
    TokenReuseDetected,
    Logout,
    LogoutAll
}
