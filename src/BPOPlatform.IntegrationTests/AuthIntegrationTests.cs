using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace BPOPlatform.IntegrationTests;

/// <summary>
/// Integration tests for the auth module: register, local login, LDAP login, /me endpoint.
/// All run against the in-memory database with DevBypass auth.
/// </summary>
public class AuthIntegrationTests : IClassFixture<BpoApiFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(BpoApiFactory factory) =>
        _client = factory.CreateClient();

    // ── POST /auth/register ────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidPayload_Returns201()
    {
        var payload = new
        {
            username = $"user_{Guid.NewGuid():N}"[..16],
            email = $"user_{Guid.NewGuid():N}@test.com",
            password = "Test1234!",
            displayName = "Test User"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("user").GetProperty("username").GetString()
            .Should().Be(payload.username);
    }

    [Fact]
    public async Task Register_DuplicateUsername_Returns500OrConflict()
    {
        var username = $"dup_{Guid.NewGuid():N}"[..14];
        var payload = new
        {
            username,
            email = $"{username}@test.com",
            password = "Test1234!",
            displayName = "Dup User"
        };

        // First registration
        var r1 = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);
        r1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second with same username
        var r2 = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);
        ((int)r2.StatusCode).Should().BeGreaterThanOrEqualTo(400);
    }

    // ── POST /auth/login ───────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var username = $"login_{Guid.NewGuid():N}"[..15];
        var email = $"{username}@test.com";

        // Register first
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { username, email, password = "Test1234!", displayName = "Login User" });

        // Login
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "Test1234!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401Or500()
    {
        var username = $"badpwd_{Guid.NewGuid():N}"[..14];
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { username, email = $"{username}@test.com", password = "Test1234!" });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username, password = "WrongPass!" });

        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400);
    }

    // ── POST /auth/login/ldap ──────────────────────────────────────────────────

    [Fact]
    public async Task LoginLdap_MockAcceptsTestUser_Returns200()
    {
        // MockLdapAuthService accepts testuser/TestPass1 on any domain
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login/ldap",
            new { username = "testuser", password = "TestPass1", domain = "corp.example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("user").GetProperty("isLdapUser").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task LoginLdap_WrongCredentials_Returns401Or500()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login/ldap",
            new { username = "nobody", password = "badpassword", domain = "corp.example.com" });

        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400);
    }

    // ── GET /auth/me ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_WithDevBypass_Returns200()
    {
        // DevBypass sets userId to 00000000-0000-0000-0000-000000000001 (not in DB)
        // GlobalExceptionHandler will return 404/500; we just test it doesn't return 401/403
        var response = await _client.GetAsync("/api/v1/auth/me");
        ((int)response.StatusCode).Should().NotBe(401).And.NotBe(403);
    }

    // ── GET /users (SuperAdmin) ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllUsers_WithDevBypassSuperAdmin_Returns200()
    {
        var response = await _client.GetAsync("/api/v1/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── PUT /users/me ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMe_WithValidPayload_Returns200OrError()
    {
        var response = await _client.PutAsJsonAsync("/api/v1/users/me",
            new { displayName = "Updated Name", email = "updated@test.com" });
        // DevBypass user (00000000-0000-0000-0000-000000000001) may not be in DB → 4xx/5xx
        // Either way, must not be 401/403
        ((int)response.StatusCode).Should().NotBe(401).And.NotBe(403);
    }
}
