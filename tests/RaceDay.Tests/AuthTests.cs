using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>User registration and login, authenticated vs unauthenticated access.</summary>
public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public AuthTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Participant_ReturnsCreated_AndNeverReturnsPassword()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.Registration(RoleNames.Participant));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Participant", body);
    }

    [Fact]
    public async Task Register_Organiser_WithCorrectInviteCode_ReturnsCreated()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", TestHelpers.Registration(RoleNames.Organiser));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutInviteCode_IsForbidden()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Organiser);
        request.OrganiserCode = null;

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongInviteCode_IsForbidden()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Organiser);
        request.OrganiserCode = "WRONG-CODE";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Participant);

        var first = await client.PostAsJsonAsync("/api/auth/register", request);
        var second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Participant, "not-an-email");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Participant);
        request.Role = "Admin";

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresPasswordHashed_NotPlainText()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Participant);
        await client.PostAsJsonAsync("/api/auth/register", request);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayContext>();
        var stored = db.Users.Single(u => u.Email == request.Email.ToLowerInvariant());

        Assert.NotEqual(request.Password, stored.PasswordHash);
        Assert.DoesNotContain(request.Password, stored.PasswordHash);
        Assert.True(stored.PasswordHash.Length > 20);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOk_SetsSessionCookie_AndGrantsAccess()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Participant);
        await client.PostAsJsonAsync("/api/auth/register", request);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = request.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("RaceDay.Session=", StringComparison.Ordinal));

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal(RoleNames.Participant, body!.Role);

        // The same client now carries the session and can reach a protected endpoint.
        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = TestHelpers.Registration(RoleNames.Participant);
        await client.PostAsJsonAsync("/api/auth/register", request);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = request.Email, Password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "nobody@raceday-tests.co.za", Password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = await _factory.LoggedInClientAsync(RoleNames.Participant);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var after = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesOwnName()
    {
        var client = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var update = new UpdateProfileRequest { FullName = "Updated Name", Email = TestHelpers.UniqueEmail("updated") };

        var response = await client.PutAsJsonAsync("/api/users/me", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<UserResponse>("/api/users/me");
        Assert.Equal("Updated Name", me!.FullName);
    }
}
//ST10486532
//Author: Gift Tshwaane