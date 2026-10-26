using Etamid.Application.Common;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Services;

/// <summary>Builds the user object returned by login, verify and GET /me.</summary>
public sealed class UserProfileService(IApplicationDbContext db, IFileUrlResolver files)
{
    public async Task<UserDto> BuildAsync(User user, CancellationToken ct)
    {
        CompanyInfoDto? company = null;
        if (user.Type == UserType.Company)
        {
            var profile = await db.CompanyProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == user.Id, ct);
            if (profile is not null) company = new CompanyInfoDto(profile.CommercialRegNo, profile.TaxNo);
        }

        var walletActivated = await db.Wallets.AsNoTracking().AnyAsync(w => w.UserId == user.Id && w.Activated, ct);

        string? avatarUrl = null;
        if (user.AvatarFileId is { } avatarId)
        {
            var avatar = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == avatarId, ct);
            avatarUrl = files.Resolve(avatar);
        }

        BankAccountDto? bankDto = null;
        var account = await db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == user.Id, ct);
        if (account is not null)
        {
            var bank = await db.Banks.AsNoTracking().FirstAsync(b => b.Id == account.BankId, ct);
            string? logo = null;
            if (bank.LogoFileId is { } logoId)
                logo = files.Resolve(await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == logoId, ct));

            bankDto = new BankAccountDto(
                account.HolderName,
                "SA" + new string('•', 9) + account.IbanLast4,
                LanguageHelper.Pick(bank.NameAr, bank.NameEn),
                logo);
        }

        return new UserDto(
            user.Id, user.Code, user.Type, user.Name, user.Phone, user.Email, avatarUrl, user.Language,
            user.PhoneVerifiedAt is not null, user.NafathVerified, walletActivated, bankDto, company);
    }

    public async Task<AuthResultDto> ToAuthResultAsync(TokenPair pair, CancellationToken ct)
        => new(pair.AccessToken, pair.RefreshToken, pair.ExpiresInSeconds, await BuildAsync(pair.User, ct));
}
