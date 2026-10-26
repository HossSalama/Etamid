namespace Etamid.Domain.Identity.Services;

/// <summary>Rules for accepting a Firebase phone-verification token.</summary>
public static class PhoneVerificationPolicy
{
    /// <summary>The user must have completed the SMS check at most this long ago.</summary>
    public static readonly TimeSpan MaxTokenAge = TimeSpan.FromMinutes(5);

    public static bool IsFresh(DateTime authTimeUtc, DateTime nowUtc)
        => authTimeUtc <= nowUtc.AddMinutes(1) && nowUtc - authTimeUtc <= MaxTokenAge;

    /// <summary>A token whose auth time is not newer than the last accepted one is a replay.</summary>
    public static bool IsReplay(DateTime authTimeUtc, DateTime? lastAcceptedUtc)
        => lastAcceptedUtc.HasValue && authTimeUtc <= lastAcceptedUtc.Value;
}
