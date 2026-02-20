using BPOPlatform.Domain.Common;
using BPOPlatform.Domain.Enums;

namespace BPOPlatform.Domain.Entities;

/// <summary>
/// Platform user – supports local password authentication and LDAP/AD domain authentication.
/// Role is either <see cref="Roles.SuperAdmin"/> (full access) or <see cref="Roles.User"/>
/// (own-data access only).
/// </summary>
public class ApplicationUser : BaseEntity
{
    // Private setters – use factory methods to maintain invariants.
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }

    // Password auth fields (null for LDAP-only users)
    public string? PasswordHash { get; private set; }
    public string? PasswordSalt { get; private set; }

    // Role: "SuperAdmin" or "User"
    public string Role { get; private set; } = Roles.User;

    public bool IsActive { get; private set; } = true;

    // LDAP / domain fields
    public bool IsLdapUser { get; private set; }
    public string? LdapDomain { get; private set; }

    public DateTime? LastLoginAt { get; private set; }

    // ── Factory methods ────────────────────────────────────────────────────────

    /// <summary>Creates a new local (password) user.</summary>
    public static ApplicationUser CreateLocal(
        string username,
        string email,
        string passwordHash,
        string passwordSalt,
        string role = Roles.User,
        string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Username = username.ToLowerInvariant().Trim(),
            Email = email.ToLowerInvariant().Trim(),
            DisplayName = displayName ?? username,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Role = role,
            IsLdapUser = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Creates a new LDAP/AD user (first login via domain).</summary>
    public static ApplicationUser CreateLdap(
        string username,
        string email,
        string ldapDomain,
        string role = Roles.User,
        string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(ldapDomain);

        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Username = username.ToLowerInvariant().Trim(),
            Email = email.ToLowerInvariant().Trim(),
            DisplayName = displayName ?? username,
            Role = role,
            IsLdapUser = true,
            LdapDomain = ldapDomain,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── Mutation methods ───────────────────────────────────────────────────────

    public void RecordLogin() => LastLoginAt = DateTime.UtcNow;

    public void UpdatePassword(string passwordHash, string passwordSalt)
    {
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string? displayName, string? email)
    {
        if (displayName is not null) DisplayName = displayName;
        if (email is not null) Email = email.ToLowerInvariant().Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetRole(string role)
    {
        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.UtcNow; }
}
