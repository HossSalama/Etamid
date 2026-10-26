using Etamid.Domain.Identity.Services;
using Etamid.Domain.Identity.ValueObjects;
using FluentValidation;

namespace Etamid.Application.Identity.Validation;

/// <summary>Reusable rules that always emit the stable error codes used by the API contract.</summary>
public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string> MustBeSaudiPhone<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithErrorCode("REQUIRED")
            .Must(p => PhoneNumber.TryParse(p, out _)).WithErrorCode("INVALID_PHONE");

    public static IRuleBuilderOptions<T, string> MustBeEmail<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(190).WithErrorCode("TOO_LONG")
            .EmailAddress().WithErrorCode("INVALID_EMAIL");

    public static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithErrorCode("REQUIRED")
            .Must(PasswordPolicy.IsValid).WithErrorCode("WEAK_PASSWORD");

    public static IRuleBuilderOptions<T, string> MustBeRequired<T>(this IRuleBuilder<T, string> rule, int maxLength)
        => rule
            .NotEmpty().WithErrorCode("REQUIRED")
            .MaximumLength(maxLength).WithErrorCode("TOO_LONG");
}
