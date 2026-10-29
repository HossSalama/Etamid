using Etamid.Application.Common;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Dtos;
using Etamid.Application.Identity.Errors;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.Services;
using Etamid.Domain.Identity.ValueObjects;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Services;

public sealed record CompanyRegistration(string CommercialRegNo, string? TaxNo);

/// <summary>Creates (or refreshes) a pending account and sends its activation code to the email address.</summary>
public sealed class RegistrationService(
    IApplicationDbContext db,
    IPasswordHasher hasher,
    UserCodeGenerator codes,
    AuthAuditService audit,
    IRequestContext request,
    EmailOtpService otps)
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

        if (existing is not null && (existing.Status != AccountStatus.PendingVerification || existing.Type != type))
            return IdentityErrors.PhoneExists;

        var existingId = existing?.Id ?? 0;
        var emailChanged = existing is not null && !string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase);

        if (await db.Users.AnyAsync(u => u.Email == email && u.DeletedAt == null && u.Id != existingId, ct))
            return IdentityErrors.EmailExists;

        if (company is not null)
        {
            var regNo = company.CommercialRegNo.Trim();
            if (await db.CompanyProfiles.AnyAsync(c => c.CommercialRegNo == regNo && c.UserId != existingId, ct))
                return IdentityErrors.CommercialRegExists;
        }

        // Never retarget a still-valid code to a different email during the resend cooldown.
        if (emailChanged && existing is not null)
        {
            var wait = await otps.CooldownSecondsAsync(existing.Id, OtpPurpose.Registration, ct);
            if (wait > 0) return IdentityErrors.OtpCooldown(wait);
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

        var issued = await otps.IssueAsync(user, OtpPurpose.Registration, ct);
        // Same-email retry inside cooldown reuses the code already delivered. Changed-email retries fail closed.
        if (issued.IsFailure && emailChanged) return issued.Error;

        return new RegisterResultDto(phone.Local, user.Email, "email", true);
    }
}
