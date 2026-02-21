using Microsoft.AspNetCore.Authorization;

namespace BPOPlatform.Api.Middleware;

/// <summary>
/// Development-only <see cref="IAuthorizationPolicyProvider"/> that returns a pass-through
/// (always-succeed) policy for every request. Registered as a singleton <b>only</b> when
/// <c>ASPNETCORE_ENVIRONMENT=Development</c>, replacing the default
/// <see cref="DefaultAuthorizationPolicyProvider"/>.
///
/// This is the most robust way to bypass [Authorize] in Development because it intercepts
/// policy resolution at the source, regardless of what other packages (e.g. Microsoft.Identity.Web)
/// configure on the authentication or authorization pipeline.
/// </summary>
public sealed class DevPermissivePolicyProvider : IAuthorizationPolicyProvider
{
    /// <summary>A single shared pass-through policy (RequireAssertion(_ => true)).</summary>
    private static readonly AuthorizationPolicy _allowAll =
        new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();

    /// <inheritdoc />
    /// <remarks>Called for every named policy referenced by <c>[Authorize(Policy = "...")]</c>.</remarks>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        => Task.FromResult<AuthorizationPolicy?>(_allowAll);

    /// <inheritdoc />
    /// <remarks>Called for bare <c>[Authorize]</c> (no policy name).</remarks>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => Task.FromResult(_allowAll);

    /// <inheritdoc />
    /// <remarks>Called for endpoints that have no explicit authorization requirement.</remarks>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => Task.FromResult<AuthorizationPolicy?>(_allowAll);
}
