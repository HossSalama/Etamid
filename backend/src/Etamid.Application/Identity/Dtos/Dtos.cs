using Etamid.Domain.Identity;

namespace Etamid.Application.Identity.Dtos;

/// <summary>Registration creates the account; it is activated after the code sent to <paramref name="Email"/> is verified.</summary>
public sealed record RegisterResultDto(string Phone, string Email, string VerificationMethod, bool VerificationRequired);

public sealed record ResetTokenDto(string ResetToken, int ExpiresInSeconds);

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
    bool EmailVerified,
    bool NafathVerified,
    bool WalletActivated,
    BankAccountDto? BankAccount,
    CompanyInfoDto? Company);

public sealed record AuthResultDto(string AccessToken, string RefreshToken, int ExpiresInSeconds, UserDto User);

public sealed record TermsDto(string Version, string Title, IReadOnlyList<string> Items);
