using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Event enrolments and results.</summary>
public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    private async Task<(HttpClient Organiser, EventResponse Event, CategoryResponse Category)> SetUpEventAsync(int maxParticipants = 100)
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var category = await organiser.CreateCategoryAsync(ev.EventID, max: maxParticipants);
        return (organiser, ev, category);
    }

    [Fact]
    public async Task Participant_CanEnrol_AndTheEnrolmentLinksParticipantEventAndCategory()
    {
        var (_, ev, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var response = await participant.PostAsync($"/api/categories/{category.CategoryID}/enrol", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await response.Content.ReadFromJsonAsync<EnrolmentResponse>();
        Assert.Equal(category.CategoryID, enrolment!.CategoryID);
        Assert.Equal(ev.EventID, enrolment.EventID);
        Assert.Equal(EnrolmentStatuses.Confirmed, enrolment.Status);

        // The enrolment is recorded: it shows up in the participant's own list.
        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments");
        Assert.Contains(mine!, e => e.EnrolmentID == enrolment.EnrolmentID && e.EventID == ev.EventID);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForOwnEvent()
    {
        var (organiser, ev, category) = await SetUpEventAsync();
        var p1 = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var p2 = await _factory.LoggedInClientAsync(RoleNames.Participant);
        await p1.EnrolAsync(category.CategoryID);
        await p2.EnrolAsync(category.CategoryID);

        var roster = await organiser.GetFromJsonAsync<List<EnrolmentResponse>>($"/api/events/{ev.EventID}/enrolments");

        Assert.Equal(2, roster!.Count);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsOfAnotherOrganisersEvent()
    {
        var (_, ev, _) = await SetUpEventAsync();
        var otherOrganiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);

        var response = await otherOrganiser.GetAsync($"/api/events/{ev.EventID}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_SameCategoryTwice_ReturnsConflict()
    {
        var (_, _, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        await participant.EnrolAsync(category.CategoryID);

        var again = await participant.PostAsync($"/api/categories/{category.CategoryID}/enrol", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Enrol_SecondCategoryInSameEvent_ReturnsConflict()
    {
        var (organiser, ev, first) = await SetUpEventAsync();
        var second = await organiser.CreateCategoryAsync(ev.EventID, "5km Fun Run");
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        await participant.EnrolAsync(first.CategoryID);

        var response = await participant.PostAsync($"/api/categories/{second.CategoryID}/enrol", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_ReturnsNotFound()
    {
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var response = await participant.PostAsync("/api/categories/999999/enrol", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenCategoryIsFull_ReturnsConflict()
    {
        var (_, _, category) = await SetUpEventAsync(maxParticipants: 1);
        var first = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var second = await _factory.LoggedInClientAsync(RoleNames.Participant);
        await first.EnrolAsync(category.CategoryID);

        var response = await second.PostAsync($"/api/categories/{category.CategoryID}/enrol", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_AndReEnrolAfterwards()
    {
        var (_, _, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await participant.EnrolAsync(category.CategoryID);

        var cancel = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentID}");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentResponse>>("/api/users/me/enrolments");
        Assert.Equal(EnrolmentStatuses.Cancelled, mine!.Single().Status);

        var reEnrol = await participant.PostAsync($"/api/categories/{category.CategoryID}/enrol", null);
        Assert.Equal(HttpStatusCode.Created, reEnrol.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment()
    {
        var (_, _, category) = await SetUpEventAsync();
        var owner = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var other = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await owner.EnrolAsync(category.CategoryID);

        var response = await other.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentID}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanViewOwnResult()
    {
        var (organiser, ev, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await participant.EnrolAsync(category.CategoryID);

        var capture = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/result",
            new ResultRequest { FinishTime = new TimeOnly(0, 52, 14), OverallPosition = 3, CategoryPosition = 1, Status = ResultStatuses.Finished });
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");
        var result = Assert.Single(mine!);
        Assert.Equal(new TimeOnly(0, 52, 14), result.FinishTime);
        Assert.Equal(3, result.OverallPosition);
        Assert.Equal(ev.EventID, result.EventID);

        // The public leaderboard shows it too.
        var leaderboard = await _factory.CreateClient().GetFromJsonAsync<List<ResultResponse>>($"/api/events/{ev.EventID}/results");
        Assert.Single(leaderboard!);
    }

    [Fact]
    public async Task Participant_OnlySeesTheirOwnResults()
    {
        var (organiser, _, category) = await SetUpEventAsync();
        var runner = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var bystander = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await runner.EnrolAsync(category.CategoryID);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/result",
            new ResultRequest { FinishTime = new TimeOnly(0, 45, 0), OverallPosition = 1, CategoryPosition = 1, Status = ResultStatuses.Finished });

        var theirs = await bystander.GetFromJsonAsync<List<ResultResponse>>("/api/users/me/results");

        Assert.Empty(theirs!);
    }

    [Fact]
    public async Task CaptureResult_Twice_ReturnsConflict_AndOrganiserCanEditInstead()
    {
        var (organiser, _, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await participant.EnrolAsync(category.CategoryID);
        var request = new ResultRequest { FinishTime = new TimeOnly(1, 0, 0), OverallPosition = 5, CategoryPosition = 2, Status = ResultStatuses.Finished };

        var first = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/result", request);
        var second = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/result", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var created = await first.Content.ReadFromJsonAsync<ResultResponse>();
        request.OverallPosition = 4;
        var edit = await organiser.PutAsJsonAsync($"/api/results/{created!.ResultID}", request);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await edit.Content.ReadFromJsonAsync<ResultResponse>();
        Assert.Equal(4, edited!.OverallPosition);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutTime_ReturnsBadRequest()
    {
        var (organiser, _, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await participant.EnrolAsync(category.CategoryID);

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/result",
            new ResultRequest { FinishTime = null, OverallPosition = 1, CategoryPosition = 1, Status = ResultStatuses.Finished });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultForAnotherOrganisersEvent()
    {
        var (_, _, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        var enrolment = await participant.EnrolAsync(category.CategoryID);
        var otherOrganiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);

        var response = await otherOrganiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentID}/result",
            new ResultRequest { FinishTime = new TimeOnly(0, 40, 0), OverallPosition = 1, CategoryPosition = 1, Status = ResultStatuses.Finished });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_ReturnsConflict()
    {
        var (organiser, ev, category) = await SetUpEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);
        await participant.EnrolAsync(category.CategoryID);

        var response = await organiser.DeleteAsync($"/api/events/{ev.EventID}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
//ST10486532
//Author: Gift Tshwaane