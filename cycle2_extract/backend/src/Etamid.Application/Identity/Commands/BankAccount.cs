using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Services;
using Etamid.Application.Identity.Validation;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.Services;
using Etamid.SharedKernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Commands;

public sealed record GetMyBankAccountQuery : IRequest<Result<BankAccountDto?>>;

public sealed class GetMyBankAccountHandler(UserProfileService profiles, ICurrentUser currentUser)
    : IRequestHandler<GetMyBankAccountQuery, Result<BankAccountDto?>>
{
    public async Task<Result<BankAccountDto?>> Handle(GetMyBankAccountQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;
        return Result.Success(await profiles.BuildBankAccountAsync(userId, ct));
    }
}

public sealed record SaveBankAccountCommand(string HolderName, string Iban) : IRequest<Result<BankAccountDto?>>;

public sealed class SaveBankAccountValidator : AbstractValidator<SaveBankAccountCommand>
{
    public SaveBankAccountValidator()
    {
        RuleFor(x => x.HolderName).MustBeRequired(150);
        RuleFor(x => x.Iban)
            .NotEmpty().WithErrorCode("REQUIRED")
            .Must(IbanPolicy.IsValid).WithErrorCode("INVALID_IBAN");
    }
}

public sealed class SaveBankAccountHandler(
    IApplicationDbContext db, ICurrentUser currentUser, IFieldProtector protector, IClock clock, UserProfileService profiles)
    : IRequestHandler<SaveBankAccountCommand, Result<BankAccountDto?>>
{
    public async Task<Result<BankAccountDto?>> Handle(SaveBankAccountCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var iban = IbanPolicy.Normalize(r.Iban);
        var bankCode = IbanPolicy.BankCode(iban);

        var bank = await db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.Code == bankCode && b.IsActive, ct);
        if (bank is null) return Error.Validation("UNSUPPORTED_BANK", "iban");

        var hash = protector.Hash(iban);
        if (await db.BankAccounts.AnyAsync(a => a.IbanHash == hash && a.UserId != userId, ct))
            return Error.Conflict("IBAN_EXISTS", "iban");

        var account = await db.BankAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (account is null)
        {
            account = new BankAccount { UserId = userId };
            db.BankAccounts.Add(account);
        }

        account.HolderName = r.HolderName.Trim();
        account.IbanEncrypted = protector.Protect(iban);
        account.IbanLast4 = IbanPolicy.Last4(iban);
        account.IbanHash = hash;
        account.BankId = bank.Id;
        account.UpdatedAt = clock.UtcNow;
        // A changed IBAN has to be re-verified by an admin or the payout flow.
        account.VerifiedAt = null;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race with another user saving the same IBAN.
            return Error.Conflict("IBAN_EXISTS", "iban");
        }

        return Result.Success(await profiles.BuildBankAccountAsync(userId, ct));
    }
}
