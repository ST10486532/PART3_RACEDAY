using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Data behind the MVC dashboards: organiser summary and "47th out of 312 finishers".</summary>
public class DashboardTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public DashboardTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_SeesOnlyOwnEvents_WithEnrolmentTotals()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var someoneElse = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var mine = await organiser.CreateEventAsync("My Dashboard Event");
        await someoneElse.CreateEventAsync("Not Mine");
        var category = await organiser.CreateCategoryAsync(mine.EventID);
        var p1 = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var p2 = await _factory.LoggedInClientAsync(RoleNames.Participant);
        await p1.EnrolAsync(category.CategoryID);
        await p2.EnrolAsync(category.CategoryID);

        var summary = await organiser.GetFromJsonAsync<List<OrganiserEventSummary>>("/api/events/mine");

        var row = Assert.Single(summary!);
        Assert.Equal("My Dashboard Event", row.EventName);
        Assert.Equal(2, row.ConfirmedEnrolments);
        Assert.Equal(1, row.CategoryCount);
    }

    [Fact]
    public async Task CancelledEnrolments_AreNotCountedInTheSummary()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync("Cancel Count");
        var category = await organiser.CreateCategoryAsync(ev.EventID);
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await participant.EnrolAsync(category.CategoryID);
        await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentID}");

        var summary = await organiser.GetFromJsonAsync<List<OrganiserEventSummary>>("/api/events/mine");

        Assert.Equal(0, Assert.Single(summary!).ConfirmedEnrolments);
    }

    [Fact]
    public async Task Participant_CannotUseOrganiserSummary()
    {
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var response = await participant.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyResults_IncludeEventDate_AndTotalFinishers()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync("Finishers Event");
        var category = await organiser.CreateCategoryAsync(ev.EventID);
        var runner1 = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var runner2 = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment1 = await runner1.EnrolAsync(category.CategoryID);
        var enrolment2 = await runner2.EnrolAsync(category.CategoryID);

        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment1.EnrolmentID}/result",
            new ResultRequest { FinishTime = new TimeOnly(0, 40, 0), OverallPosition = 1, CategoryPosition = 1, Status = ResultStatuses.Finished });
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment2.EnrolmentID}/result",
            new ResultRequest { FinishTime = new TimeOnly(0, 45, 0), OverallPosition = 2, CategoryPosition = 2, Status = ResultStatuses.Finished });

        var results = await runner2.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");

        var mine = Assert.Single(results!);
        Assert.Equal(2, mine.OverallPosition);
        Assert.Equal(2, mine.TotalFinishers);
        Assert.Equal(ev.EventDate, mine.EventDate);
    }

    [Fact]
    public async Task DidNotFinish_IsNotCountedAsAFinisher()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync("DNF Event");
        var category = await organiser.CreateCategoryAsync(ev.EventID);
        var finisher = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var dnf = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var finisherEnrolment = await finisher.EnrolAsync(category.CategoryID);
        var dnfEnrolment = await dnf.EnrolAsync(category.CategoryID);

        await organiser.PostAsJsonAsync($"/api/enrolments/{finisherEnrolment.EnrolmentID}/result",
            new ResultRequest { FinishTime = new TimeOnly(0, 50, 0), OverallPosition = 1, CategoryPosition = 1, Status = ResultStatuses.Finished });
        await organiser.PostAsJsonAsync($"/api/enrolments/{dnfEnrolment.EnrolmentID}/result",
            new ResultRequest { Status = ResultStatuses.DNF });

        var results = await finisher.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");

        Assert.Equal(1, Assert.Single(results!).TotalFinishers);
    }
}
//ST10486532
//Author: Gift Tshwaane