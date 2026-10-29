using Etamid.Domain.Common;

namespace Etamid.Domain.Identity;

public enum OtpPurpose { Registration, PasswordReset }

/// <summary>
/// A one-time code sent by email. Only a keyed hash of the code is stored.
/// For password reset the verified code is exchanged for a single-use reset token (also stored hashed).
/// </summary>
public sealed class EmailOtp : CreatedEntity<long>
{
    public long UserId { get; set; }
    public OtpPurpose Purpose { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public string? ResetTokenHash { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    public DateTime? ResetTokenUsedAt { get; set; }
}
