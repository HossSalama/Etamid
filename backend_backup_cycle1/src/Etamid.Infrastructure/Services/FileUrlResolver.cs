using Etamid.Application.Common.Interfaces;
using Etamid.Domain.Files;
using Microsoft.Extensions.Configuration;

namespace Etamid.Infrastructure.Services;

/// <summary>Turns a stored file's storage key into a public URL (CDN / bucket base URL from configuration).</summary>
public sealed class FileUrlResolver(IConfiguration configuration) : IFileUrlResolver
{
    public string? Resolve(StoredFile? file)
    {
        if (file is null) return null;
        var baseUrl = configuration["Storage:PublicBaseUrl"] ?? "https://cdn.example.com";
        return $"{baseUrl.TrimEnd('/')}/{file.StorageKey.TrimStart('/')}";
    }
}
