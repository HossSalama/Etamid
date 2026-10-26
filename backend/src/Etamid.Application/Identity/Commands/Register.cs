using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Identity;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;

namespace Etamid.Application.Identity.Commands;

// ---------- Individual
public sealed record RegisterIndividualCommand(string Username, string Phone, string Email, string Password, bool AcceptTerms)
    : IRequest<Result<RegisterResultDto>>;

public sealed class RegisterIndividualValidator : AbstractValidator<RegisterIndividualCommand>
{
    public RegisterIndividualValidator()
    {
        RuleFor(x => x.Username).MustBeRequired(150);
        RuleFor(x => x.Phone).MustBeSaudiPhone();
        RuleFor(x => x.Email).MustBeEmail();
        RuleFor(x => x.Password).MustBeStrongPassword();
        RuleFor(x => x.AcceptTerms).Equal(true).WithErrorCode("TERMS_NOT_ACCEPTED");
    }
}

public sealed class RegisterIndividualHandler(RegistrationService registration)
    : IRequestHandler<RegisterIndividualCommand, Result<RegisterResultDto>>
{
    public Task<Result<RegisterResultDto>> Handle(RegisterIndividualCommand r, CancellationToken ct)
        => registration.RegisterAsync(UserType.Individual, r.Username, r.Phone, r.Email, r.Password, null, ct);
}

// ---------- Company
public sealed record RegisterCompanyCommand(
    string CompanyName, string OwnerPhone, string CommercialRegNo, string? TaxNo, string Email, string Password, bool AcceptTerms)
    : IRequest<Result<RegisterResultDto>>;

public sealed class RegisterCompanyValidator : AbstractValidator<RegisterCompanyCommand>
{
    public RegisterCompanyValidator()
    {
        RuleFor(x => x.CompanyName).MustBeRequired(200);
        RuleFor(x => x.OwnerPhone).MustBeSaudiPhone();
        RuleFor(x => x.CommercialRegNo).MustBeRequired(30);
        RuleFor(x => x.TaxNo!).MaximumLength(30).WithErrorCode("TOO_LONG").When(x => x.TaxNo is not null);
        RuleFor(x => x.Email).MustBeEmail();
        RuleFor(x => x.Password).MustBeStrongPassword();
        RuleFor(x => x.AcceptTerms).Equal(true).WithErrorCode("TERMS_NOT_ACCEPTED");
    }
}

public sealed class RegisterCompanyHandler(RegistrationService registration)
    : IRequestHandler<RegisterCompanyCommand, Result<RegisterResultDto>>
{
    public Task<Result<RegisterResultDto>> Handle(RegisterCompanyCommand r, CancellationToken ct)
        => registration.RegisterAsync(
            UserType.Company, r.CompanyName, r.OwnerPhone, r.Email, r.Password,
            new CompanyRegistration(r.CommercialRegNo, r.TaxNo), ct);
}
