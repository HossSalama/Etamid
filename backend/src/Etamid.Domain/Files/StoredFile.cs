using Etamid.Domain.Common;

namespace Etamid.Domain.Files;

public enum FilePurpose { Avatar, StoreBanner, DealImage, Dispute, Contract, Support, Other }
public enum FileStatus { Uploaded, Attached, Quarantined, Deleted }

/// <summary>Table "Files". Only metadata lives in the DB; the bytes are in object storage.</summary>
public sealed class StoredFile : CreatedEntity<long>
{
    public Guid PublicId { get; set; }
    public long? OwnerUserId { get; set; }
    public FilePurpose Purpose { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public FileStatus Status { get; set; }
}
