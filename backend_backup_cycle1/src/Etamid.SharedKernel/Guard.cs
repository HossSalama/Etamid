namespace Etamid.SharedKernel;

public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string paramName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be null or whitespace.", paramName)
            : value;

    public static T AgainstNull<T>(T? value, string paramName) where T : class
        => value ?? throw new ArgumentNullException(paramName);

    public static decimal AgainstNegativeOrZero(decimal value, string paramName)
        => value <= 0 ? throw new ArgumentOutOfRangeException(paramName, "Value must be greater than zero.") : value;
}
