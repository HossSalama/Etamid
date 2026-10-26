using Etamid.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Etamid.Infrastructure.Services;

public sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
