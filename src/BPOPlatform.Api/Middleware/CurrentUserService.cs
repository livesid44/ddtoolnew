using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BPOPlatform.Api.Middleware;

/// <summary>
/// Reads the current user's identity from the ASP.NET Core <see cref="IHttpContextAccessor"/>.
/// Works with both the LocalJwt scheme and the DevBypass scheme.
/// </summary>
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Principal?.FindFirst("sub")?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Username =>
        Principal?.FindFirst(ClaimTypes.Name)?.Value
        ?? Principal?.FindFirst("preferred_username")?.Value;

    public string? Role =>
        Principal?.FindFirst(ClaimTypes.Role)?.Value
        ?? Principal?.FindFirst("roles")?.Value;

    public bool IsSuperAdmin =>
        string.Equals(Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
        || Principal?.IsInRole(Roles.SuperAdmin) == true;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}

