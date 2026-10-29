using System.Security.Cryptography;
using Etamid.Application.Common.Interfaces;
using Etamid.Application.Identity.Errors;
using Etamid.Domain.Identity;
using Etamid.Domain.Identity.Services;
using Etamid.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Etamid.Application.Identity.Services;

/// <summary>Creates, sends and validates persisted email codes; only keyed hashes are stored.</summary>
public sealed class EmailOtpService(
    IApplicationDbContext db, IFieldProtector protector, IEmailService email, IClock clock)
{
    public async Task<int> CooldownSecondsAsync(long userId, OtpPurpose purpose, CancellationToken ct)
    {
        var last = await db.EmailOtps
            .Where(o => o.UserId == userId && o.Purpose == purpose)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(ct);

        if (last is null) return 0;

        // ExpiresAt is based on IClock, unlike CreatedAt which may be stamped by persistence.
        var issuedAt = last.ExpiresAt - EmailOtpPolicy.Lifetime;
        var wait = EmailOtpPolicy.ResendCooldown - (clock.UtcNow - issuedAt);
        return wait > TimeSpan.Zero ? (int)Math.Ceiling(wait.TotalSeconds) : 0;
    }

    /// <summary>Invalidates prior open codes, persists a new hash and sends the code by email.</summary>
    public async Task<Result> IssueAsync(User user, OtpPurpose purpose, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var cooldown = await CooldownSecondsAsync(user.Id, purpose, ct);
        if (cooldown > 0) return IdentityErrors.OtpCooldown(cooldown);

        var open = await db.EmailOtps
            .Where(o => o.UserId == user.Id && o.Purpose == purpose && o.ConsumedAt == null)
            .ToListAsync(ct);
        foreach (var previous in open) previous.ConsumedAt = now;

        if (purpose == OtpPurpose.PasswordReset)
        {
            // A new reset request invalidates any previously issued, unused reset token.
            var priorResetTokens = await db.EmailOtps
                .Where(o => o.UserId == user.Id && o.Purpose == purpose
                    && o.ResetTokenHash != null && o.ResetTokenUsedAt == null)
                .ToListAsync(ct);
            foreach (var prior in priorResetTokens) prior.ResetTokenUsedAt = now;
        }

        var code = EmailOtpPolicy.NewCode();
        db.EmailOtps.Add(new EmailOtp
        {
            UserId = user.Id,
            Purpose = purpose,
            CodeHash = HashCode(user.Id, purpose, user.Email, code),
            ExpiresAt = now + EmailOtpPolicy.Lifetime
        });
        await db.SaveChangesAsync(ct);

        await email.SendOtpEmailAsync(
            user.Email,
            user.Name,
            code,
            purpose == OtpPurpose.PasswordReset ? OtpEmailKind.PasswordReset : OtpEmailKind.Registration,
            ct);

        return Result.Success();
    }

    /// <summary>Checks the latest open code. Every incorrect attempt is counted and eventually locks that code.</summary>
    public async Task<Result<EmailOtp>> CheckAsync(User user, OtpPurpose purpose, string code, CancellationToken ct)
    {
        if (!EmailOtpPolicy.IsWellFormed(code)) return IdentityErrors.OtpInvalid;

        var now = clock.UtcNow;
        var otp = await db.EmailOtps
            .Where(o => o.UserId == user.Id && o.Purpose == purpose && o.ConsumedAt == null)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(ct);

        if (otp is null) return IdentityErrors.OtpInvalid;
        if (otp.ExpiresAt <= now) return IdentityErrors.OtpExpired;
        if (otp.Attempts >= EmailOtpPolicy.MaxAttempts) return IdentityErrors.OtpAttemptsExceeded;

        var expected = Convert.FromHexString(otp.CodeHash);
        var actual = Convert.FromHexString(HashCode(user.Id, purpose, user.Email, code));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            otp.Attempts++;
            await db.SaveChangesAsync(ct);
            return IdentityErrors.OtpInvalid;
        }

        return otp;
    }

    private string HashCode(long userId, OtpPurpose purpose, string emailAddress, string code)
        => protector.Hash($"otp:{userId}:{purpose}:{emailAddress.Trim().ToLowerInvariant()}:{code}");
}
