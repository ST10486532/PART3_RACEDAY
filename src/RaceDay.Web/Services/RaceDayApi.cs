using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// HTTP client for the RaceDay API. After login the API replies with its session cookie; we keep that cookie in the
/// MVC user's server-side session and send it back on every call, so the API always knows who is calling and their role.
/// </summary>
public class RaceDayApi : IRaceDayApi
{
    private const string ApiCookieName = "RaceDay.Session=";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<RaceDayApi> _logger;

    public RaceDayApi(HttpClient http, IHttpContextAccessor accessor, ILogger<RaceDayApi> logger)
    {
        _http = http;
        _accessor = accessor;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // Authentication and profile
    // ------------------------------------------------------------------

    public async Task<ApiResult<LoginResultDto>> LoginAsync(string email, string password)
    {
        var result = await SendAsync<LoginResultDto>(HttpMethod.Post, "api/auth/login",
            JsonContent.Create(new { email, password }, options: Json), throwOnAuthErrors: false);

        if (result.IsSuccess && string.IsNullOrEmpty(_accessor.HttpContext?.Session.GetString(SessionKeys.ApiCookie)))
        {
            return ApiResult<LoginResultDto>.Failure(502, "Login worked but the API did not start a session. Please try again.");
        }
        return result;
    }

    public Task<ApiResult<UserDto>> RegisterAsync(RegisterPayload payload) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/register", JsonContent.Create(payload, options: Json), throwOnAuthErrors: false);

    public async Task LogoutAsync()
    {
        try
        {
            await SendAsync<MessageDto>(HttpMethod.Post, "api/auth/logout", null, throwOnAuthErrors: false);
        }
        catch (ApiUnavailableException)
        {
            // Logging out locally must still work even if the API is down.
        }
    }

    public Task<ApiResult<UserDto>> GetProfileAsync() =>
        SendAsync<UserDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserDto>> UpdateProfileAsync(ProfilePayload payload) =>
        SendAsync<UserDto>(HttpMethod.Put, "api/users/me", JsonContent.Create(payload, options: Json), throwOnAuthErrors: true);

    public Task<ApiResult<UserDto>> UploadProfilePictureAsync(IFormFile file) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/users/me/profile-picture", BuildFileForm(file));

    public Task<ApiFile?> GetProfilePictureAsync() => GetFileAsync("api/users/me/profile-picture");

    // ------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(string? type, string? location)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(type)) query.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(location)) query.Add($"location={Uri.EscapeDataString(location)}");
        var url = query.Count > 0 ? $"api/events?{string.Join("&", query)}" : "api/events";
        return SendAsync<List<EventDto>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventDetailDto>> GetEventAsync(int eventId) =>
        SendAsync<EventDetailDto>(HttpMethod.Get, $"api/events/{eventId}");

    public Task<ApiResult<List<OrganiserEventDto>>> GetMyEventsAsync() =>
        SendAsync<List<OrganiserEventDto>>(HttpMethod.Get, "api/events/mine");

    public Task<ApiResult<EventDto>> CreateEventAsync(EventPayload payload) =>
        SendAsync<EventDto>(HttpMethod.Post, "api/events", JsonContent.Create(payload, options: Json));

    public Task<ApiResult<EventDto>> UpdateEventAsync(int eventId, EventPayload payload) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{eventId}", JsonContent.Create(payload, options: Json));

    public Task<ApiResult<MessageDto>> DeleteEventAsync(int eventId) =>
        SendAsync<MessageDto>(HttpMethod.Delete, $"api/events/{eventId}");

    public Task<ApiResult<EventDto>> UploadBannerAsync(int eventId, IFormFile file) =>
        SendAsync<EventDto>(HttpMethod.Post, $"api/events/{eventId}/banner", BuildFileForm(file));

    public Task<ApiFile?> GetBannerAsync(int eventId) => GetFileAsync($"api/events/{eventId}/banner");

    // ------------------------------------------------------------------
    // Categories
    // ------------------------------------------------------------------

    public Task<ApiResult<CategoryDto>> AddCategoryAsync(int eventId, CategoryPayload payload) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", JsonContent.Create(payload, options: Json));

    public Task<ApiResult<CategoryDto>> UpdateCategoryAsync(int categoryId, CategoryPayload payload) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", JsonContent.Create(payload, options: Json));

    public Task<ApiResult<MessageDto>> DeleteCategoryAsync(int categoryId) =>
        SendAsync<MessageDto>(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ------------------------------------------------------------------
    // Enrolments
    // ------------------------------------------------------------------

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, $"api/categories/{categoryId}/enrol");

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/users/me/enrolments");

    public Task<ApiResult<MessageDto>> CancelEnrolmentAsync(int enrolmentId) =>
        SendAsync<MessageDto>(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    // ------------------------------------------------------------------
    // Results
    // ------------------------------------------------------------------

    public Task<ApiResult<ResultDto>> CaptureResultAsync(int enrolmentId, ResultPayload payload) =>
        SendAsync<ResultDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/result", JsonContent.Create(payload, options: Json));

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, ResultPayload payload) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}", JsonContent.Create(payload, options: Json));

    public Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId) =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/events/{eventId}/results");

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() =>
        SendAsync<List<ResultDto>>(HttpMethod.Get, "api/users/me/results");

    // ------------------------------------------------------------------
    // Plumbing
    // ------------------------------------------------------------------

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null, bool throwOnAuthErrors = true)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        AttachSessionCookie(request);

        using var response = await SendRawAsync(request);
        var status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(Json);
                return value is null
                    ? ApiResult<T>.Failure(status, "The server sent an empty response.")
                    : ApiResult<T>.Success(value, status);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Could not read the API response for {Method} {Url}", method, url);
                return ApiResult<T>.Failure(status, "The server sent a response we could not understand.");
            }
        }

        if (throwOnAuthErrors)
        {
            if (status == (int)HttpStatusCode.Unauthorized) throw new ApiUnauthorizedException();
            if (status == (int)HttpStatusCode.Forbidden) throw new ApiForbiddenException();
        }

        return ApiResult<T>.Failure(status, await ReadErrorMessageAsync(response));
    }

    private async Task<ApiFile?> GetFileAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AttachSessionCookie(request);

        using var response = await SendRawAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new ApiFile(bytes, contentType);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request)
    {
        try
        {
            var response = await _http.SendAsync(request);
            StoreSessionCookie(response);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "The RaceDay API could not be reached.");
            throw new ApiUnavailableException(ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "The RaceDay API did not answer in time.");
            throw new ApiUnavailableException(ex);
        }
    }

    /// <summary>Sends the API session cookie saved in the MVC session (if the user is logged in).</summary>
    private void AttachSessionCookie(HttpRequestMessage request)
    {
        var cookie = _accessor.HttpContext?.Session.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
    }

    /// <summary>If the API sent (or refreshed/cleared) its session cookie, remember that in the MVC session.</summary>
    private void StoreSessionCookie(HttpResponseMessage response)
    {
        var session = _accessor.HttpContext?.Session;
        if (session == null || !response.Headers.TryGetValues("Set-Cookie", out var headers)) return;

        foreach (var header in headers)
        {
            var pair = header.Split(';', 2)[0].Trim();
            if (!pair.StartsWith(ApiCookieName, StringComparison.Ordinal)) continue;

            if (pair.Length == ApiCookieName.Length)
                session.Remove(SessionKeys.ApiCookie);          // API ended the session
            else
                session.SetString(SessionKeys.ApiCookie, pair); // new or refreshed session
            return;
        }
    }

    private static MultipartFormDataContent BuildFileForm(IFormFile file)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        if (MediaTypeHeaderValue.TryParse(file.ContentType, out var contentType))
        {
            fileContent.Headers.ContentType = contentType;
        }
        form.Add(fileContent, "file", Path.GetFileName(file.FileName));
        return form;
    }

    /// <summary>Turns the API's problem-details JSON into one short, friendly sentence.</summary>
    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var field in errors.EnumerateObject())
                {
                    foreach (var message in field.Value.EnumerateArray())
                    {
                        var text = message.GetString();
                        if (!string.IsNullOrWhiteSpace(text)) messages.Add(text);
                    }
                }
                if (messages.Count > 0) return string.Join(" ", messages);
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString() ?? "Something went wrong.";

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString() ?? "Something went wrong.";
        }
        catch (Exception)
        {
            // fall through to the generic message
        }
        return "Something went wrong. Please try again.";
    }
}
//ST10486532
//Author: Gift Tshwaane