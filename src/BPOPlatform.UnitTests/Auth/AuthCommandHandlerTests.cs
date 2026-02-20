using BPOPlatform.Application.Auth.Commands;
using BPOPlatform.Application.Auth.DTOs;
using BPOPlatform.Domain.Entities;
using BPOPlatform.Domain.Enums;
using BPOPlatform.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace BPOPlatform.UnitTests.Auth;

public class AuthCommandHandlerTests
{
    // ── Shared helpers ──────────────────────────────────────────────────────────

    private static ApplicationUser MakeLocalUser()
    {
        var hasher = new BPOPlatform.Infrastructure.Services.PasswordHasherService();
        var (hash, salt) = hasher.HashPassword("Test1234!");
        return ApplicationUser.CreateLocal("testuser", "test@test.com", hash, salt);
    }

    private static ApplicationUser MakeLdapUser() =>
        ApplicationUser.CreateLdap("ldapuser", "ldap@test.com", "test.com");

    // ── RegisterUserCommand ─────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterUser_CreatesUserAndReturnsToken()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<ApplicationUser>(), default))
            .Returns(Task.CompletedTask);

        var handler = new RegisterUserCommandHandler(
            repo.Object,
            new BPOPlatform.Infrastructure.Services.PasswordHasherService(),
            new JwtTokenServiceFake(),
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new RegisterUserCommand("newuser", "new@test.com", "Test1234!", "New User"), default);

        result.Should().NotBeNull();
        result.AccessToken.Should().Be("fake-token");
        result.User.Username.Should().Be("newuser");
        result.User.Role.Should().Be(Roles.User);
    }

    [Fact]
    public async Task RegisterUser_ThrowsWhenDuplicate()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(true);

        var handler = new RegisterUserCommandHandler(
            repo.Object,
            new BPOPlatform.Infrastructure.Services.PasswordHasherService(),
            new JwtTokenServiceFake(),
            new Mock<IUnitOfWork>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new RegisterUserCommand("x", "x@x.com", "Test1234!", null), default));
    }

    // ── LoginCommand ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var user = MakeLocalUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByUsernameAsync("testuser", default)).ReturnsAsync(user);

        var handler = new LoginCommandHandler(
            repo.Object,
            new BPOPlatform.Infrastructure.Services.PasswordHasherService(),
            new JwtTokenServiceFake(),
            new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new LoginCommand("testuser", "Test1234!"), default);

        result.AccessToken.Should().Be("fake-token");
        result.User.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        var user = MakeLocalUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByUsernameAsync("testuser", default)).ReturnsAsync(user);

        var handler = new LoginCommandHandler(
            repo.Object,
            new BPOPlatform.Infrastructure.Services.PasswordHasherService(),
            new JwtTokenServiceFake(),
            new Mock<IUnitOfWork>().Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new LoginCommand("testuser", "WrongPass!"), default));
    }

    [Fact]
    public async Task Login_LdapUser_ThrowsUnauthorized()
    {
        var user = MakeLdapUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByUsernameAsync("ldapuser", default)).ReturnsAsync(user);

        var handler = new LoginCommandHandler(
            repo.Object,
            new BPOPlatform.Infrastructure.Services.PasswordHasherService(),
            new JwtTokenServiceFake(),
            new Mock<IUnitOfWork>().Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new LoginCommand("ldapuser", "anything"), default));
    }

    // ── LoginLdapCommand ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginLdap_Success_ProvisionsAndReturnsToken()
    {
        var ldap = new Mock<ILdapAuthService>();
        ldap.Setup(l => l.AuthenticateAsync("testuser", "pass", "corp.com", default))
            .ReturnsAsync((true, "testuser@corp.com", "Test User"));

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByUsernameAsync("testuser", default)).ReturnsAsync((ApplicationUser?)null);
        repo.Setup(r => r.AddAsync(It.IsAny<ApplicationUser>(), default)).Returns(Task.CompletedTask);

        var handler = new LoginLdapCommandHandler(
            repo.Object, ldap.Object, new JwtTokenServiceFake(), new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new LoginLdapCommand("testuser", "pass", "corp.com"), default);

        result.AccessToken.Should().Be("fake-token");
        result.User.IsLdapUser.Should().BeTrue();
        result.User.LdapDomain.Should().Be("corp.com");
    }

    [Fact]
    public async Task LoginLdap_InvalidCreds_ThrowsUnauthorized()
    {
        var ldap = new Mock<ILdapAuthService>();
        ldap.Setup(l => l.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), default))
            .ReturnsAsync((false, (string?)null, (string?)null));

        var handler = new LoginLdapCommandHandler(
            new Mock<IUserRepository>().Object,
            ldap.Object,
            new JwtTokenServiceFake(),
            new Mock<IUnitOfWork>().Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(new LoginLdapCommand("user", "bad", "corp.com"), default));
    }

    // ── ChangePasswordCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ValidCurrentPassword_Succeeds()
    {
        var user = MakeLocalUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var hasher = new BPOPlatform.Infrastructure.Services.PasswordHasherService();
        var handler = new ChangePasswordCommandHandler(
            repo.Object, hasher, new Mock<IUnitOfWork>().Object);

        await handler.Handle(new ChangePasswordCommand(user.Id, "Test1234!", "NewPass5678!"), default);

        hasher.VerifyPassword("NewPass5678!", user.PasswordHash!, user.PasswordSalt!)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_LdapUser_Throws()
    {
        var user = MakeLdapUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var handler = new ChangePasswordCommandHandler(
            repo.Object,
            new BPOPlatform.Infrastructure.Services.PasswordHasherService(),
            new Mock<IUnitOfWork>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ChangePasswordCommand(user.Id, "any", "NewPass1!"), default));
    }

    // ── DeleteUserCommand ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_DeactivatesUser()
    {
        var user = MakeLocalUser();
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var handler = new DeleteUserCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);
        await handler.Handle(new DeleteUserCommand(user.Id), default);

        user.IsActive.Should().BeFalse();
    }

    // ── Fake JWT service ────────────────────────────────────────────────────────

    private class JwtTokenServiceFake : IJwtTokenService
    {
        public string GenerateToken(ApplicationUser user) => "fake-token";
    }
}
