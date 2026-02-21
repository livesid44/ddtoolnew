namespace BPOPlatform.Application.Auth.DTOs;

/// <summary>Returned after a successful login or registration.</summary>
public record LoginResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    UserDto User);

/// <summary>Public representation of an application user.</summary>
public record UserDto(
    Guid Id,
    string Username,
    string Email,
    string? DisplayName,
    string Role,
    bool IsLdapUser,
    string? LdapDomain,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
