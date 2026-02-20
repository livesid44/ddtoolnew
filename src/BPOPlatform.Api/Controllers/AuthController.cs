using BPOPlatform.Application.Auth.Commands;
using BPOPlatform.Application.Auth.Queries;
using BPOPlatform.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BPOPlatform.Api.Controllers;

/// <summary>
/// Authentication endpoints: register, local login, LDAP login, and get current user.
/// POST /auth/register and POST /auth/login are public (no token required).
/// GET /auth/me requires an authenticated user.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController(IMediator mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Register a new local (password) user account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Application.Auth.DTOs.LoginResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Login with username and password (local accounts only).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Application.Auth.DTOs.LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Login with LDAP/Active Directory credentials.</summary>
    [HttpPost("login/ldap")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Application.Auth.DTOs.LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginLdap([FromBody] LoginLdapCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Get the currently authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(Application.Auth.DTOs.UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot determine user identity.");
        var result = await mediator.Send(new GetCurrentUserQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>Change the current user's password (local accounts only).</summary>
    [HttpPut("me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body, CancellationToken ct)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot determine user identity.");
        await mediator.Send(new ChangePasswordCommand(userId, body.CurrentPassword, body.NewPassword), ct);
        return NoContent();
    }
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
