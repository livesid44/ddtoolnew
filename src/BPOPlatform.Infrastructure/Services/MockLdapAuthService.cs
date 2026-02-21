using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BPOPlatform.Infrastructure.Services;

/// <summary>
/// Development fallback LDAP service.
/// Accepts <c>testuser / TestPass1</c> on any domain; rejects everything else.
/// This allows the full LDAP login flow to be exercised locally without an LDAP server.
/// </summary>
public class MockLdapAuthService(ILogger<MockLdapAuthService> logger) : ILdapAuthService
{
    public Task<(bool Success, string? Email, string? DisplayName)> AuthenticateAsync(
        string username, string password, string domain, CancellationToken ct = default)
    {
        const string AcceptUser = "testuser";
        const string AcceptPass = "TestPass1";

        if (username.Equals(AcceptUser, StringComparison.OrdinalIgnoreCase) && password == AcceptPass)
        {
            logger.LogInformation("[MockLdap] Authenticated {Username}@{Domain}", username, domain);
            return Task.FromResult<(bool, string?, string?)>(
                (true, $"{username}@{domain}", "Test User (LDAP)"));
        }

        logger.LogInformation("[MockLdap] Rejected {Username}@{Domain}", username, domain);
        return Task.FromResult<(bool, string?, string?)>((false, null, null));
    }
}
