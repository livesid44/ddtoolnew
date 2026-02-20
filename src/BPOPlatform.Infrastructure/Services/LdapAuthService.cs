using BPOPlatform.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;

namespace BPOPlatform.Infrastructure.Services;

public class LdapSettings
{
    public const string SectionName = "Ldap";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public string BaseDn { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Authenticates users against an LDAP/Active Directory server by attempting a bind
/// with the supplied credentials.  Supports both plain LDAP and LDAPS (TLS).
/// </summary>
public class LdapAuthService(
    IOptions<LdapSettings> options,
    ILogger<LdapAuthService> logger) : ILdapAuthService
{
    private readonly LdapSettings _settings = options.Value;

    public async Task<(bool Success, string? Email, string? DisplayName)> AuthenticateAsync(
        string username, string password, string domain, CancellationToken ct = default)
    {
        // Build the bind DN — try userPrincipalName (UPN) first (AD-style)
        var bindDn = username.Contains('@') ? username : $"{username}@{domain}";

        try
        {
            using var conn = new LdapConnection { SecureSocketLayer = _settings.UseSsl };
            conn.ConnectionTimeout = _settings.TimeoutSeconds * 1000;
            await Task.Run(() => conn.Connect(_settings.Host, _settings.Port), ct);
            await Task.Run(() => conn.Bind(bindDn, password), ct);

            if (!conn.Bound)
                return (false, null, null);

            // Optionally search for email/displayName attributes
            string? email = null;
            string? displayName = null;

            if (!string.IsNullOrWhiteSpace(_settings.BaseDn))
            {
                try
                {
                    var filter = $"(sAMAccountName={EscapeLdap(username)})";
                    var search = await Task.Run(
                        () => conn.Search(
                            _settings.BaseDn,
                            LdapConnection.ScopeSub,
                            filter,
                            ["mail", "displayName"],
                            typesOnly: false),
                        ct);

                    if (search.HasMore())
                    {
                        var entry = search.Next();
                        email = entry.GetAttribute("mail")?.StringValue;
                        displayName = entry.GetAttribute("displayName")?.StringValue;
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: attributes are optional
                    logger.LogWarning(ex, "LDAP attribute lookup failed for {Username}", username);
                }
            }

            return (true, email, displayName);
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            logger.LogInformation("LDAP authentication failed for {Username}: invalid credentials", username);
            return (false, null, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LDAP authentication error for {Username}", username);
            return (false, null, null);
        }
    }

    /// <summary>Escape special LDAP filter characters per RFC 4515.</summary>
    private static string EscapeLdap(string value) =>
        value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
}
