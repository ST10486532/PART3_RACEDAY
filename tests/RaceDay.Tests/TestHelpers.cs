using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Shared helpers so each test reads like the scenario it checks.</summary>
public static class TestHelpers
{
    public const string Password = "Passw0rd!";
    private static int _counter;

    public static string UniqueEmail(string prefix) =>
        $"{prefix}{Interlocked.Increment(ref _counter)}@raceday-tests.co.za";

    public static RegisterRequest Registration(string role, string? email = null) => new()
    {
        FullName = $"Test {role}",
        Email = email ?? UniqueEmail(role.ToLowerInvariant()),
        Password = Password,
        Role = role,
        OrganiserCode = role == RoleNames.Organiser ? RaceDayFactory.OrganiserCode : null
    };

    public static EventRequest NewEvent(string name = "Test City Run") => new()
    {
        EventName = name,
        EventDate = new DateOnly(2026, 11, 8),
        Location = "Sandton, Johannesburg",
        EventType = EventTypes.Run,
        DistanceKm = 10m,
        Description = "A test event."
    };

    public static CategoryRequest NewCategory(string name = "10km Challenge", int max = 100) => new()
    {
        CategoryName = name,
        DistanceKm = 10m,
        EntryFee = 150m,
        MaxParticipants = max
    };

    /// <summary>Registers a new user and logs them in. The returned client carries the session cookie.</summary>
    public static async Task<HttpClient> LoggedInClientAsync(this RaceDayFactory factory, string role)
    {
        var client = factory.CreateClient();
        var registration = Registration(role);

        var register = await client.PostAsJsonAsync("/api/auth/register", registration);
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = registration.Email, Password = registration.Password });
        login.EnsureSuccessStatusCode();

        return client;
    }

    public static async Task<EventResponse> CreateEventAsync(this HttpClient organiser, string name = "Test City Run")
    {
        var response = await organiser.PostAsJsonAsync("/api/events", NewEvent(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventResponse>())!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(this HttpClient organiser, int eventId, string name = "10km Challenge", int max = 100)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", NewCategory(name, max));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    public static async Task<EnrolmentResponse> EnrolAsync(this HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrol", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }
}
//ST10486532
//Author: Gift Tshwaane