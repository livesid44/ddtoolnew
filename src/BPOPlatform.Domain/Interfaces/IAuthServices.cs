using BPOPlatform.Domain.Entities;

namespace BPOPlatform.Domain.Interfaces;

// ── User Repository ────────────────────────────────────────────────────────────

/// <summary>Persistence interface for <see cref="ApplicationUser"/>.</summary>
public interface IUserRepository : IRepository<ApplicationUser>
{
    Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationUser>> GetAllActiveAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(string username, string email, CancellationToken ct = default);
}

// ── Auth service interfaces ────────────────────────────────────────────────────

/// <summary>Generates and validates JWT tokens for local/LDAP authenticated users.</summary>
public interface IJwtTokenService
{
    /// <summary>Creates a signed JWT token for the given user.</summary>
    string GenerateToken(ApplicationUser user);
}

/// <summary>PBKDF2 password hashing and verification.</summary>
public interface IPasswordHasherService
{
    (string Hash, string Salt) HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string hash, string salt);
}

/// <summary>Authenticates users against an LDAP/Active Directory server.</summary>
public interface ILdapAuthService
{
    /// <summary>
    /// Attempts to bind with the provided credentials.
    /// Returns (success, email, displayName) on success.
    /// </summary>
    Task<(bool Success, string? Email, string? DisplayName)> AuthenticateAsync(
        string username,
        string password,
        string domain,
        CancellationToken ct = default);
}

/// <summary>Provides information about the currently authenticated user.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    bool IsSuperAdmin { get; }
    bool IsAuthenticated { get; }
}
