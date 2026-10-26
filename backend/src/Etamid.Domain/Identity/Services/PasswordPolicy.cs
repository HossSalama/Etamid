namespace Etamid.Domain.Identity.Services;

/// <summary>Business rule: at least 6 characters (letters or digits) plus at least one symbol such as #.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 6;
    public const int MaxLength = 64;

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        if (password.Length is < MinLength or > MaxLength) return false;
        if (password.Any(char.IsWhiteSpace)) return false;
        return password.Any(c => !char.IsLetterOrDigit(c));
    }
}
