using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Everything the MVC app can ask of the RaceDay API. All data goes through here.</summary>
public interface IRaceDayApi
{
    // ----- authentication and profile -----
    Task<ApiResult<LoginResultDto>> LoginAsync(string email, string password);
    Task<ApiResult<UserDto>> RegisterAsync(RegisterPayload payload);
    Task LogoutAsync();
    Task<ApiResult<UserDto>> GetProfileAsync();
    Task<ApiResult<UserDto>> UpdateProfileAsync(ProfilePayload payload);
    Task<ApiResult<UserDto>> UploadProfilePictureAsync(IFormFile file);
    Task<ApiFile?> GetProfilePictureAsync();

    // ----- events -----
    Task<ApiResult<List<EventDto>>> GetEventsAsync(string? type, string? location);
    Task<ApiResult<EventDetailDto>> GetEventAsync(int eventId);
    Task<ApiResult<List<OrganiserEventDto>>> GetMyEventsAsync();
    Task<ApiResult<EventDto>> CreateEventAsync(EventPayload payload);
    Task<ApiResult<EventDto>> UpdateEventAsync(int eventId, EventPayload payload);
    Task<ApiResult<MessageDto>> DeleteEventAsync(int eventId);
    Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file);
    Task<ApiFile?> GetBannerAsync(int eventId);

    // ----- categories -----
    Task<ApiResult<CategoryDto>> AddCategoryAsync(int eventId, CategoryPayload payload);
    Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryPayload payload);
    Task<ApiResult<MessageDto>> DeleteCategoryAsync(int categoryId);

    // ----- enrolments -----
    Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId);
    Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync();
    Task<ApiResult<MessageDto>> CancelEnrolmentAsync(int enrolmentId);
    Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId);

    // ----- results -----
    Task<ApiResult<ResultDto>> CaptureResultAsync(int enrolmentId, ResultPayload payload);
    Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultPayload payload);
    Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId);
    Task<ApiResult<List<ResultDto>>> GetMyResultsAsync();
}
//ST10486532
//Author: Gift Tshwaane