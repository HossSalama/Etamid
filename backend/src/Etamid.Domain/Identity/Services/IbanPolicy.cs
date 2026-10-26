namespace Etamid.Domain.Identity.Services;

/// <summary>Saudi IBAN rules: "SA" + 22 digits (24 chars), ISO 13616 mod-97 checksum. Digits 5-6 identify the bank.</summary>
public static class IbanPolicy
{
    public const int Length = 24;

    /// <summary>Removes spaces and upper-cases. Does not validate.</summary>
    public static string Normalize(string? input)
        => (input ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

    public static bool IsValid(string? input)
    {
        var iban = Normalize(input);
        if (iban.Length != Length || !iban.StartsWith("SA", StringComparison.Ordinal)) return false;
        for (var i = 2; i < iban.Length; i++)
            if (iban[i] is < '0' or > '9') return false;

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var ch in rearranged)
        {
            var value = ch is >= '0' and <= '9' ? ch - '0' : ch - 'A' + 10;
            remainder = value < 10
                ? (remainder * 10 + value) % 97
                : (remainder * 100 + value) % 97;
        }
        return remainder == 1;
    }

    /// <summary>The 2-digit bank identifier (e.g. "80" = Al Rajhi). The IBAN must already be normalized and valid.</summary>
    public static string BankCode(string normalizedIban) => normalizedIban.Substring(4, 2);

    public static string Last4(string normalizedIban) => normalizedIban[^4..];
}
