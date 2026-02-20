using BPOPlatform.Application.Auth.Commands;
using BPOPlatform.Application.Auth.DTOs;
using BPOPlatform.Application.Auth.Queries;
using BPOPlatform.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

/// <summary>
/// User management endpoints.
/// GET /users – list all users (SuperAdmin only).
/// DELETE /users/{id} – deactivate a user (SuperAdmin only).
/// PUT /users/me – update the current user's own profile.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class UsersController(IMediator mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>List all active users. SuperAdmin only.</summary>
    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllUsersQuery(), ct);
        return Ok(result);
    }

    /// <summary>Deactivate (soft-delete) a user. SuperAdmin only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteUserCommand(id), ct);
        return NoContent();
    }

    /// <summary>Update the current user's own profile (displayName, email).</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest body, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot determine user identity.");

        // Fetch user, update, return updated DTO
        var user = await mediator.Send(new GetCurrentUserQuery(userId), ct);
        // Profile update is lightweight – use the repository directly via a command
        await mediator.Send(new UpdateProfileCommand(userId, body.DisplayName, body.Email), ct);
        var updated = await mediator.Send(new GetCurrentUserQuery(userId), ct);
        return Ok(updated);
    }
}

public record UpdateProfileRequest(string? DisplayName, string? Email);
