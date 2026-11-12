using System.Security.Claims;
using Etamid.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Etamid.Infrastructure.Services;

/// <summary>Reads the admin principal. Only a principal with type = "admin" counts; app-user tokens never do.</summary>
public sealed class CurrentAdmin(IHttpContextAccessor accessor) : ICurrentAdmin
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    private bool IsAdminPrincipal
        => Principal?.Identity?.IsAuthenticated == true && Principal.FindFirstValue("type") == "admin";

    public bool IsAuthenticated => IsAdminPrincipal;

    public long? AdminId
        => IsAdminPrincipal && long.TryParse(Principal!.FindFirstValue("sub"), out var id) ? id : null;

    public Guid? SessionId
        => IsAdminPrincipal && Guid.TryParse(Principal!.FindFirstValue("sid"), out var sid) ? sid : null;
}
