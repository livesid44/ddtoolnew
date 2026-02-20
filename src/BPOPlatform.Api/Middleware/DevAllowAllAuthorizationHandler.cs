using Microsoft.AspNetCore.Authorization;

namespace BPOPlatform.Api.Middleware;

/// <summary>
/// Development-only authorization handler that approves ALL authorization requirements.
/// Registered in Development so that every [Authorize]-protected endpoint is accessible
/// without a real JWT token. Never registered in Production or Staging.
/// </summary>
public class DevAllowAllAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements.ToList())
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
