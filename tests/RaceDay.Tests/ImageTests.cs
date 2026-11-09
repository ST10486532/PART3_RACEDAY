using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>Image uploads (event banners and profile pictures) stored through the storage abstraction.</summary>
public class ImageTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public ImageTests(RaceDayFactory factory) => _factory = factory;

    // Smallest thing that starts like a real PNG: the file signature plus padding.
    private static byte[] PngBytes() => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3 };

    private static MultipartFormDataContent FileContent(byte[] bytes, string contentType = "image/png", string fileName = "image.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Organiser_CanUploadBanner_AndAnyoneCanDownloadIt()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync("Banner Event");

        var upload = await organiser.PostAsync($"/api/events/{ev.EventID}/banner", FileContent(PngBytes()));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var updated = await upload.Content.ReadFromJsonAsync<EventResponse>();
        Assert.True(updated!.HasBanner);

        var download = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventID}/banner");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngBytes(), await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ReplacingBanner_RemovesTheOldBlob()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync("Replace Banner");
        var storage = (InMemoryImageStorage)_factory.Services.GetRequiredService<IImageStorage>();

        await organiser.PostAsync($"/api/events/{ev.EventID}/banner", FileContent(PngBytes()));
        var afterFirst = storage.Count;
        await organiser.PostAsync($"/api/events/{ev.EventID}/banner", FileContent(PngBytes()));

        Assert.Equal(afterFirst, storage.Count);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var response = await participant.PostAsync($"/api/events/{ev.EventID}/banner", FileContent(PngBytes()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent()
    {
        var owner = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var other = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await owner.CreateEventAsync();

        var response = await other.PostAsync($"/api/events/{ev.EventID}/banner", FileContent(PngBytes()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotUploadBanner()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();

        var response = await _factory.CreateClient().PostAsync($"/api/events/{ev.EventID}/banner", FileContent(PngBytes()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ThatIsNotAnImage_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("this is just text pretending to be a picture");

        var response = await organiser.PostAsync($"/api/events/{ev.EventID}/banner", FileContent(notAnImage, "image/png", "fake.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ThatIsTooLarge_ReturnsBadRequest()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();
        var big = new byte[ImageValidator.MaxBytes + 1];
        PngBytes().CopyTo(big, 0);

        var response = await organiser.PostAsync($"/api/events/{ev.EventID}/banner", FileContent(big));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBanner_WhenEventHasNone_ReturnsNotFound()
    {
        var organiser = await _factory.LoggedInClientAsync(RoleNames.Organiser);
        var ev = await organiser.CreateEventAsync();

        var response = await _factory.CreateClient().GetAsync($"/api/events/{ev.EventID}/banner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_CanUploadAndDownloadOwnProfilePicture()
    {
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var upload = await participant.PostAsync("/api/users/me/profile-picture", FileContent(PngBytes()));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var me = await participant.GetFromJsonAsync<UserResponse>("/api/users/me");
        Assert.True(me!.HasProfilePicture);

        var download = await participant.GetAsync("/api/users/me/profile-picture");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(PngBytes(), await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ProfilePicture_RequiresLogin()
    {
        var anonymous = _factory.CreateClient();

        var upload = await anonymous.PostAsync("/api/users/me/profile-picture", FileContent(PngBytes()));
        var download = await anonymous.GetAsync("/api/users/me/profile-picture");

        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, download.StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_WhenNoneUploaded_ReturnsNotFound()
    {
        var participant = await _factory.LoggedInClientAsync(RoleNames.Participant);

        var response = await participant.GetAsync("/api/users/me/profile-picture");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
//ST10486532
//Author: Gift Tshwaane