using BPOPlatform.Application.Auth.DTOs;
using BPOPlatform.Application.Auth.Mappings;
using BPOPlatform.Domain.Interfaces;
using MediatR;

namespace BPOPlatform.Application.Auth.Queries;

// ── Get current user ──────────────────────────────────────────────────────────

public record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;

public class GetCurrentUserQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User {request.UserId} not found.");
        return user.ToDto();
    }
}

// ── Get all users (SuperAdmin only – enforced in controller) ──────────────────

public record GetAllUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public class GetAllUsersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(GetAllUsersQuery request, CancellationToken ct)
    {
        var users = await userRepository.GetAllActiveAsync(ct);
        return users.Select(u => u.ToDto()).ToList();
    }
}
