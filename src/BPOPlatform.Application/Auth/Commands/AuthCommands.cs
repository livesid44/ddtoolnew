using BPOPlatform.Application.Auth.DTOs;
using BPOPlatform.Application.Auth.Mappings;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace BPOPlatform.Application.Auth.Commands;

// ── Register ──────────────────────────────────────────────────────────────────

public record RegisterUserCommand(
    string Username,
    string Email,
    string Password,
    string? DisplayName,
    string Role = Roles.User) : IRequest<LoginResponseDto>;

public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().MinimumLength(3).MaximumLength(50)
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Username may only contain letters, digits, underscores, dots, and hyphens.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password)
            .NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
        RuleFor(x => x.Role)
            .Must(r => r == Roles.User || r == Roles.SuperAdmin)
            .WithMessage($"Role must be '{Roles.User}' or '{Roles.SuperAdmin}'.");
    }
}

public class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasherService passwordHasher,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<RegisterUserCommand, LoginResponseDto>
{
    public async Task<LoginResponseDto> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var exists = await userRepository.ExistsAsync(request.Username, request.Email, ct);
        if (exists)
            throw new InvalidOperationException("A user with that username or email already exists.");

        var (hash, salt) = passwordHasher.HashPassword(request.Password);
        var user = ApplicationUser.CreateLocal(
            request.Username, request.Email, hash, salt, request.Role, request.DisplayName);

        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(ct);

        var token = jwtTokenService.GenerateToken(user);
        return BuildResponse(token, user);
    }

    private static LoginResponseDto BuildResponse(string token, ApplicationUser user) =>
        new(token, "Bearer", 3600, user.ToDto());
}

// ── Local Login ───────────────────────────────────────────────────────────────

public record LoginCommand(string Username, string Password) : IRequest<LoginResponseDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasherService passwordHasher,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginCommand, LoginResponseDto>
{
    public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username.ToLowerInvariant().Trim(), ct);
        if (user is null || !user.IsActive || user.IsLdapUser)
            throw new UnauthorizedAccessException("Invalid username or password.");

        var valid = passwordHasher.VerifyPassword(request.Password, user.PasswordHash!, user.PasswordSalt!);
        if (!valid)
            throw new UnauthorizedAccessException("Invalid username or password.");

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(ct);

        var token = jwtTokenService.GenerateToken(user);
        return new LoginResponseDto(token, "Bearer", 3600, user.ToDto());
    }
}

// ── LDAP Login ────────────────────────────────────────────────────────────────

public record LoginLdapCommand(string Username, string Password, string Domain) : IRequest<LoginResponseDto>;

public class LoginLdapCommandValidator : AbstractValidator<LoginLdapCommand>
{
    public LoginLdapCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Domain).NotEmpty();
    }
}

public class LoginLdapCommandHandler(
    IUserRepository userRepository,
    ILdapAuthService ldapAuth,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginLdapCommand, LoginResponseDto>
{
    public async Task<LoginResponseDto> Handle(LoginLdapCommand request, CancellationToken ct)
    {
        var (success, email, displayName) =
            await ldapAuth.AuthenticateAsync(request.Username, request.Password, request.Domain, ct);

        if (!success)
            throw new UnauthorizedAccessException("LDAP authentication failed. Check credentials and domain.");

        // Auto-provision LDAP user on first login
        var username = request.Username.ToLowerInvariant().Trim();
        var user = await userRepository.GetByUsernameAsync(username, ct);
        if (user is null)
        {
            user = ApplicationUser.CreateLdap(
                username,
                email ?? $"{username}@{request.Domain}",
                request.Domain,
                displayName: displayName);
            await userRepository.AddAsync(user, ct);
        }

        user.RecordLogin();
        await unitOfWork.SaveChangesAsync(ct);

        var token = jwtTokenService.GenerateToken(user);
        return new LoginResponseDto(token, "Bearer", 3600, user.ToDto());
    }
}

// ── Change Password ───────────────────────────────────────────────────────────

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must differ from the current password.");
    }
}

public class ChangePasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasherService passwordHasher,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User {request.UserId} not found.");

        if (user.IsLdapUser)
            throw new InvalidOperationException("Password cannot be changed for LDAP users.");

        var valid = passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash!, user.PasswordSalt!);
        if (!valid)
            throw new UnauthorizedAccessException("Current password is incorrect.");

        var (hash, salt) = passwordHasher.HashPassword(request.NewPassword);
        user.UpdatePassword(hash, salt);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

// ── Delete User (SuperAdmin only – enforced in controller) ────────────────────

public record DeleteUserCommand(Guid UserId) : IRequest;

public class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User {request.UserId} not found.");
        user.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);
    }
}

// ── Update Profile ─────────────────────────────────────────────────────────────

public record UpdateProfileCommand(Guid UserId, string? DisplayName, string? Email) : IRequest;

public class UpdateProfileCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateProfileCommand>
{
    public async Task Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw new KeyNotFoundException($"User {request.UserId} not found.");
        user.UpdateProfile(request.DisplayName, request.Email);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

// ── Extension ─────────────────────────────────────────────────────────────────

