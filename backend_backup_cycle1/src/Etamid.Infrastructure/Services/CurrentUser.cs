using System.Security.Claims;
using Etamid.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Etamid.Infrastructure.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long? UserId
        => long.TryParse(Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public string? UserType => Principal?.FindFirstValue("type");

    public Guid? SessionId => Guid.TryParse(Principal?.FindFirstValue("sid"), out var sid) ? sid : null;
}
