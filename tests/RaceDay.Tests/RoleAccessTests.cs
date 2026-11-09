using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Role-based access: the wrong role (or no login) is rejected on role-restricted endpoints.</summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public RoleAccessTests(RaceDayFactory factory) => _factory = factory;

    // ----- Participant trying Organiser functionality -> 403 -----

    [Fact]
    public async Task Participant_CannotCreateEvent()
    {
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var response = await participant.PostAsJsonAsync("/api/events", TestHelpers.NewEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvents()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var put = await participant.PutAsJsonAsync($"/api/events/{ev.EventID}", TestHelpers.NewEvent("Nope"));
        var delete = await participant.DeleteAsync($"/api/events/{ev.EventID}");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotManageCategories()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventID);
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var add = await participant.PostAsJsonAsync($"/api/events/{ev.EventID}/categories", TestHelpers.NewCategory("Sneaky"));
        var edit = await participant.PutAsJsonAsync($"/api/categories/{category.CategoryID}", TestHelpers.NewCategory("Sneaky"));
        var delete = await participant.DeleteAsync($"/api/categories/{category.CategoryID}");

        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolmentRoster()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var response = await participant.GetAsync($"/api/events/{ev.EventID}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventID);
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await participant.EnrolAsync(category.CategoryID);

        var response = await participant.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/result",
            new ResultRequest { FinishTime = new TimeOnly(0, 30, 0), OverallPosition = 1, CategoryPosition = 1, Status = "Finished" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ----- Organiser trying Participant functionality -> 403 -----

    [Fact]
    public async Task Organiser_CannotEnrolInAnEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventID);

        var response = await organiser.PostAsync($"/api/categories/{category.CategoryID}/enrol", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUseParticipantOnlyEndpoints()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);

        var enrolments = await organiser.GetAsync("/api/users/me/enrolments");
        var results = await organiser.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Forbidden, enrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, results.StatusCode);
    }

    // ----- Not logged in -> 401 -----

    [Fact]
    public async Task Anonymous_CannotCreateEvent()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/events", TestHelpers.NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotEnrol()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventID);
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsync($"/api/categories/{category.CategoryID}/enrol", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotViewOwnEnrolmentsOrResults()
    {
        var anonymous = _factory.CreateClient();

        var enrolments = await anonymous.GetAsync("/api/users/me/enrolments");
        var results = await anonymous.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Unauthorized, enrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, results.StatusCode);
    }

    // ----- Public endpoints stay public -----

    [Fact]
    public async Task PublicEndpoints_AreReachableWithoutLogin()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/events")).StatusCode);
    }
}
//ST10486532
//Author: Gift Tshwaane