using Etamid.Domain.Identity.Services;
using Etamid.Domain.Identity.ValueObjects;

namespace Etamid.Domain.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0501234567")]
    [InlineData("501234567")]
    [InlineData("+966501234567")]
    [InlineData("966501234567")]
    [InlineData("00966501234567")]
    public void Saudi_mobile_formats_normalize_to_e164(string input)
    {
        PhoneNumber.TryParse(input, out var phone).Should().BeTrue();
        phone!.E164.Should().Be("+966501234567");
        phone.Local.Should().Be("0501234567");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("01012345678")]
    [InlineData("0401234567")]
    [InlineData("050123456")]
    [InlineData("abc")]
    public void Invalid_numbers_are_rejected(string? input)
        => PhoneNumber.TryParse(input, out _).Should().BeFalse();
}

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("abc12#")]
    [InlineData("Pass#word1")]
    [InlineData("123456#")]
    public void Valid_passwords(string p) => PasswordPolicy.IsValid(p).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab1#")]
    [InlineData("abcdef")]
    [InlineData("abc 12#")]
    public void Invalid_passwords(string? p) => PasswordPolicy.IsValid(p).Should().BeFalse();

    [Fact]
    public void Too_long_is_invalid() => PasswordPolicy.IsValid(new string('a', 64) + "#").Should().BeFalse();
}

public class PhoneVerificationPolicyTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Fresh_token_within_five_minutes() => PhoneVerificationPolicy.IsFresh(Now.AddMinutes(-4), Now).Should().BeTrue();

    [Fact]
    public void Old_token_is_stale() => PhoneVerificationPolicy.IsFresh(Now.AddMinutes(-6), Now).Should().BeFalse();

    [Fact]
    public void Replay_is_detected_when_not_newer_than_last_accepted()
    {
        PhoneVerificationPolicy.IsReplay(Now, Now).Should().BeTrue();
        PhoneVerificationPolicy.IsReplay(Now.AddSeconds(-1), Now).Should().BeTrue();
        PhoneVerificationPolicy.IsReplay(Now.AddSeconds(1), Now).Should().BeFalse();
        PhoneVerificationPolicy.IsReplay(Now, null).Should().BeFalse();
    }
}

public class IbanPolicyTests
{
    [Theory]
    [InlineData("SA0380000000608010167519")]
    [InlineData("sa03 8000 0000 6080 1016 7519")]
    [InlineData("SA4310123456789012345678")]
    public void Valid_saudi_ibans(string iban) => IbanPolicy.IsValid(iban).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SA0380000000608010167518")]   // bad checksum
    [InlineData("SA03800000006080101675")]     // too short
    [InlineData("GB82WEST12345698765432")]     // not Saudi
    [InlineData("SA038000000060801016751A")]   // letter inside
    public void Invalid_ibans(string? iban) => IbanPolicy.IsValid(iban).Should().BeFalse();

    [Fact]
    public void Bank_code_and_last4()
    {
        var iban = IbanPolicy.Normalize("SA03 8000 0000 6080 1016 7519");
        IbanPolicy.BankCode(iban).Should().Be("80");
        IbanPolicy.Last4(iban).Should().Be("7519");
    }
}
