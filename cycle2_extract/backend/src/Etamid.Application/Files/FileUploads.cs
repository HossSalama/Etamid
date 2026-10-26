using System.Security.Cryptography;
using Etamid.Application.Common.Errors;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Files;
using Etamid.SharedKernel;
using MediatR;

namespace Etamid.Application.Files;

public static class FileErrors
{
    public static readonly Error Required = Error.Validation("FILE_REQUIRED", "file");
    public static readonly Error TooLarge = Error.Validation("FILE_TOO_LARGE", "file");
    public static readonly Error TypeNotAllowed = Error.Validation("FILE_TYPE_NOT_ALLOWED", "file");
}

public sealed record UploadedFileDto(string FileId, string Url);

/// <summary>Detects the real file type from its first bytes. The client-supplied Content-Type is never trusted.</summary>
public static class FileSniffer
{
    public static (string ContentType, string Extension, bool IsImage)? Detect(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ("image/jpeg", ".jpg", true);

        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            return ("image/png", ".png", true);

        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return ("image/webp", ".webp", true);

        if (b.Length >= 5 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F' && b[4] == '-')
            return ("application/pdf", ".pdf", false);

        return null;
    }
}

/// <summary>Validates, stores and registers an uploaded file. The caller saves the DbContext.</summary>
public sealed class FileUploadService(IApplicationDbContext db, IFileStorage storage, IClock clock)
{
    public const int MaxBytes = 10 * 1024 * 1024;

    public async Task<Result<StoredFile>> StoreAsync(
        Stream content, long ownerUserId, FilePurpose purpose, string? originalName, bool imagesOnly, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes) return FileErrors.TooLarge;
        }

        if (buffer.Length == 0) return FileErrors.Required;

        var bytes = buffer.ToArray();
        var detected = FileSniffer.Detect(bytes);
        if (detected is null || (imagesOnly && !detected.Value.IsImage)) return FileErrors.TypeNotAllowed;

        var now = clock.UtcNow;
        var publicId = Guid.NewGuid();
        var key = $"{PurposeFolder(purpose)}/{now:yyyy}/{now:MM}/{publicId:N}{detected.Value.Extension}";

        buffer.Position = 0;
        await storage.SaveAsync(key, buffer, ct);

        var name = Path.GetFileName(originalName ?? string.Empty);
        if (name.Length > 200) name = name[^200..];

        var file = new StoredFile
        {
            PublicId = publicId,
            OwnerUserId = ownerUserId,
            Purpose = purpose,
            StorageKey = key,
            FileName = string.IsNullOrWhiteSpace(name) ? publicId.ToString("N") + detected.Value.Extension : name,
            ContentType = detected.Value.ContentType,
            SizeBytes = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Status = FileStatus.Uploaded
        };

        db.Files.Add(file);
        return file;
    }

    private static string PurposeFolder(FilePurpose purpose) => purpose switch
    {
        FilePurpose.Avatar => "avatars",
        FilePurpose.StoreBanner => "banners",
        FilePurpose.DealImage => "deals",
        FilePurpose.Dispute => "disputes",
        FilePurpose.Contract => "contracts",
        FilePurpose.Support => "support",
        _ => "other"
    };
}

// ---------- POST /uploads
public sealed record UploadFileCommand(Stream Content, string? FileName, string? Purpose) : IRequest<Result<UploadedFileDto>>;

public sealed class UploadFileHandler(FileUploadService uploads, IApplicationDbContext db, ICurrentUser currentUser, IFileUrlResolver urls)
    : IRequestHandler<UploadFileCommand, Result<UploadedFileDto>>
{
    private static readonly Dictionary<string, FilePurpose> Purposes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["other"] = FilePurpose.Other,
        ["deal_image"] = FilePurpose.DealImage,
        ["dispute"] = FilePurpose.Dispute,
        ["contract"] = FilePurpose.Contract,
        ["support"] = FilePurpose.Support,
        ["store_banner"] = FilePurpose.StoreBanner
    };

    public async Task<Result<UploadedFileDto>> Handle(UploadFileCommand r, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return CommonErrors.Unauthorized;

        var purpose = FilePurpose.Other;
        if (!string.IsNullOrWhiteSpace(r.Purpose) && !Purposes.TryGetValue(r.Purpose.Trim(), out purpose))
            return Error.Validation("INVALID_VALUE", "purpose");

        var stored = await uploads.StoreAsync(r.Content, userId, purpose, r.FileName, imagesOnly: purpose == FilePurpose.StoreBanner, ct);
        if (stored.IsFailure) return stored.Error;

        await db.SaveChangesAsync(ct);
        return new UploadedFileDto(stored.Value.PublicId.ToString(), urls.Resolve(stored.Value)!);
    }
}
