namespace Etamid.Api.Controllers;

/// <summary>multipart/form-data body: the file itself plus an optional purpose.</summary>
public sealed class FileForm
{
    public IFormFile? File { get; set; }
    public string? Purpose { get; set; }
}
