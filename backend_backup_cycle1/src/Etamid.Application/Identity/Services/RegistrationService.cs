using Etamid.Application.Common;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Errors;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.ValueObjects;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Services;

public sealed record CompanyRegistration(string CommercialRegNo, string? TaxNo);

/// <summary>Creates (or refreshes) an unverified account. The phone is verified afterwards with Firebase.</summary>
public sealed class RegistrationService(
    IApplicationDbContext db,
    IPasswordHasher hasher,
    UserCodeGenerator codes,
    AuthAuditService audit,
    IRequestContext request,
    IOtpService otp)
{
    public async Task<Result<RegisterResultDto>> RegisterAsync(
        UserType type, string name, string phoneInput, string emailInput, string password,
        CompanyRegistration? company, CancellationToken ct)
    {
        if (!PhoneNumber.TryParse(phoneInput, out var phone))
            return IdentityErrors.InvalidPhone;

        var email = emailInput.Trim().ToLowerInvariant();

        var existing = await db.Users
            .Include(u => u.CompanyProfile)
            .FirstOrDefaultAsync(u => u.PhoneNormalized == phone.E164 && u.DeletedAt == null, ct);

        // A number that already finished verification (or switches account type) is taken.
        if (existing is not null && (existing.Status != AccountStatus.PendingVerification || existing.Type != type))
            return IdentityErrors.PhoneExists;

        var existingId = existing?.Id ?? 0;

        if (await db.Users.AnyAsync(u => u.Email == email && u.DeletedAt == null && u.Id != existingId, ct))
            return IdentityErrors.EmailExists;

        if (company is not null)
        {
            var regNo = company.CommercialRegNo.Trim();
            if (await db.CompanyProfiles.AnyAsync(c => c.CommercialRegNo == regNo && c.UserId != existingId, ct))
                return IdentityErrors.CommercialRegExists;
        }

        var hash = hasher.Hash(password);
        User user;

        if (existing is not null)
        {
            user = existing;
            user.RefreshUnverifiedRegistration(name, email, hash, LanguageHelper.Current);
            if (company is not null)
            {
                if (user.CompanyProfile is null)
                    user.AttachCompanyProfile(new CompanyProfile(name, phone.Local, company.CommercialRegNo, company.TaxNo));
                else
                    user.CompanyProfile.Update(name, phone.Local, company.CommercialRegNo, company.TaxNo);
            }
        }
        else
        {
            user = User.Register(type, await codes.GenerateAsync(ct), name, phone, email, hash, LanguageHelper.Current);
            if (company is not null)
                user.AttachCompanyProfile(new CompanyProfile(name, phone.Local, company.CommercialRegNo, company.TaxNo));
            db.Users.Add(user);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race against another registration with the same phone/email/commercial number.
            return IdentityErrors.PhoneExists;
        }

        var terms = await db.TermsVersions
            .Where(t => t.Type == TermsType.Platform && t.IsActive)
            .OrderByDescending(t => t.PublishedAt)
            .FirstOrDefaultAsync(ct);

        if (terms is not null)
        {
            db.TermsAcceptances.Add(new TermsAcceptance
            {
                UserId = user.Id,
                TermsVersionId = terms.Id,
                AcceptedAt = DateTime.UtcNow,
                Ip = request.IpAddress
            });
        }

        audit.Record(AuthEvent.Registered, user.Id, user.PhoneNormalized);
        await db.SaveChangesAsync(ct);

        await otp.GenerateAndSendOtpAsync(phone.E164, user.Email, user.Name, ct);

        return new RegisterResultDto(phone.Local, true);
    }
}
