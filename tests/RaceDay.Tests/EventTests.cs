using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests;

/// <summary>Event management: organisers can create, edit and delete their own events.</summary>
public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EventTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanCreateEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);

        var response = await organiser.PostAsJsonAsync("/api/events", TestHelpers.NewEvent("Create Test"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("Create Test", created!.EventName);
        Assert.Equal(EventTypes.Run, created.EventType);
        Assert.Equal(10m, created.DistanceKm);
    }

    [Fact]
    public async Task CreateEvent_WithInvalidType_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var request = TestHelpers.NewEvent();
        request.EventType = "Swim";

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutName_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var request = TestHelpers.NewEvent();
        request.EventName = "";

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var created = await organiser.CreateEventAsync("Before Update");
        var update = TestHelpers.NewEvent("After Update");
        update.Location = "Soweto";

        var response = await organiser.PutAsJsonAsync($"/api/events/{created.EventID}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.Equal("After Update", updated!.EventName);
        Assert.Equal("Soweto", updated.Location);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var created = await organiser.CreateEventAsync("To Delete");

        var delete = await organiser.DeleteAsync($"/api/events/{created.EventID}");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var afterwards = await organiser.GetAsync($"/api/events/{created.EventID}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEditAnotherOrganisersEvent()
    {
        var owner = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var other = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var created = await owner.CreateEventAsync("Owner's Event");

        var response = await other.PutAsJsonAsync($"/api/events/{created.EventID}", TestHelpers.NewEvent("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent()
    {
        var owner = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var other = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var created = await owner.CreateEventAsync("Protected Event");

        var response = await other.DeleteAsync($"/api/events/{created.EventID}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anyone_CanViewEvents_AndFilterByType()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        await organiser.CreateEventAsync("Public Run");
        var anonymous = _factory.CreateClient();

        var all = await anonymous.GetAsync("/api/events");
        var walks = await anonymous.GetFromJsonAsync<List<EventResponse>>("/api/events?type=Walk");

        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.DoesNotContain(walks!, e => e.EventType != EventTypes.Walk);
    }

    [Fact]
    public async Task GetEvent_UnknownId_ReturnsNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanAddEditAndRemoveCategories_IncludingAgeCategories()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync("Category Event");

        var ageCategory = new CategoryRequest { CategoryName = "Under 20", DistanceKm = null, EntryFee = 80m, MaxParticipants = 50 };
        var add = await organiser.PostAsJsonAsync($"/api/events/{ev.EventID}/categories", ageCategory);
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var category = await add.Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.Null(category!.DistanceKm);

        var edit = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryID}",
            new CategoryRequest { CategoryName = "Senior", DistanceKm = 21m, EntryFee = 200m, MaxParticipants = 80 });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var list = await _factory.CreateClient().GetFromJsonAsync<List<CategoryResponse>>($"/api/events/{ev.EventID}/categories");
        Assert.Single(list!);
        Assert.Equal("Senior", list![0].CategoryName);

        var remove = await organiser.DeleteAsync($"/api/categories/{category.CategoryID}");
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
    }
}
//ST10486532
//Author: Gift Tshwaane