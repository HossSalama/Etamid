using Etamid.Application.Files;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed partial class AdminController
{
    // ---------- Admin file upload (dispute documents) ----------
    // Lives with the other operator points under /api/v1/admin (the frozen
    // group), not under /admin/api. The admin has no app-user id, so the
    // stored file has no owner; purpose is fixed to dispute server-side.
    // The returned fileId is sent with POST /api/v1/admin/disputes/{id}/send-documents.

    /// <summary>Image (jpg/png/webp) or PDF up to 10MB. Send multipart/form-data with the field "file".</summary>
    [HttpPost("uploads")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileUploadService.MaxBytes + 1_048_576)]
    public async Task<IActionResult> UploadDisputeFile([FromForm] FileForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return Respond<UploadedFileDto>(FileErrors.Required);

        await using var stream = form.File.OpenReadStream();
        var result = await Sender.Send(new UploadAdminFileCommand(stream, form.File.FileName), ct);
        return Respond(result, "FILE_UPLOADED", StatusCodes.Status201Created);
    }
}
