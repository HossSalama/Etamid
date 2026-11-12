namespace Etamid.Infrastructure.Push;

/// <summary>
/// Push (Layer 2, FCM) configuration. Deliberately NOT validated on start:
/// when FCM is disabled — or the service-account file is missing — the app
/// must still boot and run with the no-op sender. The startup log service
/// reports the effective state instead of failing the boot.
/// </summary>
public sealed class FcmOptions
{
    public const string Section = "Fcm";

    /// <summary>Master switch. Default false: push stays off until the user opts in.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Absolute path to the Firebase service-account JSON file, set on the
    /// server (environment variable or server-side appsettings). Never a
    /// value in git, never a pasted JSON blob.
    /// </summary>
    public string ServiceAccountPath { get; set; } = string.Empty;

    /// <summary>
    /// Optional override for the Firebase project id. When empty, the
    /// existing <c>Firebase:ProjectId</c> value (phone verification) is reused.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;
}
