using Etamid.Application.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

[Authorize]
public sealed class UploadsController : BaseApiController
{
    /// <summary>Image (jpg/png/webp) or PDF up to 10MB. Send multipart/form-data with the field "file".</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileUploadService.MaxBytes + 1_048_576)]
    public async Task<IActionResult> Upload([FromForm] FileForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return Respond<UploadedFileDto>(FileErrors.Required);

        await using var stream = form.File.OpenReadStream();
        var result = await Sender.Send(new UploadFileCommand(stream, form.File.FileName, form.Purpose), ct);
        return Respond(result, "FILE_UPLOADED", StatusCodes.Status201Created);
    }
}
