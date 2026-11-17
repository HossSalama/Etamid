using Etamid.Application.AdminAuth;
using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Files;
using Etamid.SharedKernel;
using MediatR;

namespace Etamid.Application.Files;

/// <summary>
/// Admin file upload, for dispute documents only (16 Nov 2026): the admin has
/// no app-user id, so the stored file has no owner. Purpose is fixed to
/// Dispute here; the file is sent with POST /api/v1/admin/disputes/{id}/send-documents.
/// Images and PDFs up to 10MB, same limits as the app upload.
/// </summary>
public sealed record UploadAdminFileCommand(Stream Content, string? FileName) : IRequest<Result<UploadedFileDto>>;

public sealed class UploadAdminFileHandler(
    FileUploadService uploads, IApplicationDbContext db, AdminAccessService access, IFileUrlResolver urls)
    : IRequestHandler<UploadAdminFileCommand, Result<UploadedFileDto>>
{
    public async Task<Result<UploadedFileDto>> Handle(UploadAdminFileCommand r, CancellationToken ct)
    {
        var guard = await access.RequireOperatorAsync(Etamid.Domain.Admin.AdminPermissionCodes.DisputesDecide, ct);
        if (guard.IsFailure) return guard.Error;

        var stored = await uploads.StoreAsync(r.Content, ownerUserId: null, FilePurpose.Dispute, r.FileName,
            imagesOnly: false, ct);
        if (stored.IsFailure) return stored.Error;

        await db.SaveChangesAsync(ct);
        return new UploadedFileDto(stored.Value.PublicId.ToString(), urls.Resolve(stored.Value)!);
    }
}
