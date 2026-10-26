using System.Diagnostics.CodeAnalysis;
using Etamid.SharedKernel;

namespace Etamid.Domain.Identity.ValueObjects;

/// <summary>A Saudi mobile number. Stored normalized as +9665XXXXXXXX; shown as 05XXXXXXXX.</summary>
public sealed class PhoneNumber : ValueObject
{
    private PhoneNumber(string e164) => E164 = e164;

    /// <summary>+9665XXXXXXXX</summary>
    public string E164 { get; }

    /// <summary>05XXXXXXXX</summary>
    public string Local => "0" + E164[4..];

    public static bool TryParse(string? input, [NotNullWhen(true)] out PhoneNumber? phone)
    {
        phone = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        if (input.Any(c => !(char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '(' or ')')))
            return false;

        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());

        if (digits.StartsWith("00966", StringComparison.Ordinal)) digits = digits[5..];
        else if (digits.StartsWith("966", StringComparison.Ordinal)) digits = digits[3..];
        else if (digits.StartsWith('0')) digits = digits[1..];

        if (digits.Length != 9 || digits[0] != '5') return false;

        phone = new PhoneNumber("+966" + digits);
        return true;
    }

    public override string ToString() => E164;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return E164;
    }
}
