using Etamid.Domain.Identity;

namespace Etamid.Application.Identity.Dtos;

public sealed record RegisterResultDto(
    string Phone,
    bool PhoneVerificationRequired,
    int ResendAfterSeconds = 60,
    int ExpiresInSeconds = 300);

public sealed record ResendOtpResultDto(int ResendAfterSeconds = 60);

public sealed record BankAccountDto(string HolderName, string IbanMasked, string BankName, string? BankLogoUrl);

public sealed record CompanyInfoDto(string CommercialRegNo, string? TaxNo);

public sealed record UserDto(
    long Id,
    string Code,
    UserType Type,
    string Name,
    string Phone,
    string Email,
    string? AvatarUrl,
    Language Language,
    bool PhoneVerified,
    bool NafathVerified,
    bool WalletActivated,
    BankAccountDto? BankAccount,
    CompanyInfoDto? Company);

public sealed record AuthResultDto(string AccessToken, string RefreshToken, int ExpiresInSeconds, UserDto User);

public sealed record TermsDto(string Version, string Title, IReadOnlyList<string> Items);
