using BPOPlatform.Application.Auth.DTOs;
using BPOPlatform.Domain.Entities;

namespace BPOPlatform.Application.Auth.Mappings;

internal static class UserMappings
{
    internal static UserDto ToDto(this ApplicationUser u) =>
        new(u.Id, u.Username, u.Email, u.DisplayName, u.Role,
            u.IsLdapUser, u.LdapDomain, u.CreatedAt, u.LastLoginAt);
}
