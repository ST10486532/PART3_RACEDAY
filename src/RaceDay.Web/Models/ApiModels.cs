namespace RaceDay.Web.Models;

// These classes describe the JSON the RaceDay API sends and receives.
// The MVC app owns NO database access and NO business rules: it only calls the API.

public record UserDto(int UserID, string FullName, string Email, string Role, DateTime CreatedAt, bool HasProfilePicture);

public record LoginResultDto(int UserID, string FullName, string Role, string Message);

public record MessageDto(string Message);

public record CategoryDto(int CategoryID, int EventID, string CategoryName, decimal? DistanceKm, decimal EntryFee, int MaxParticipants);

public record RouteDto(int RouteID, int EventID, string RouteName, decimal DistanceKm, decimal? ElevationGainM, string? MapUrl);

public record EventDto(
    int EventID, int OrganiserID, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string EventType, decimal DistanceKm, string? Description, bool HasBanner);

public record EventDetailDto(
    int EventID, int OrganiserID, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string EventType, decimal DistanceKm, string? Description, bool HasBanner,
    List<CategoryDto> Categories, List<RouteDto> Routes);

public record OrganiserEventDto(
    int EventID, string EventName, DateOnly EventDate, string Location, string EventType,
    decimal DistanceKm, string? Description, bool HasBanner, int CategoryCount, int ConfirmedEnrolments);

public record EnrolmentDto(
    int EnrolmentID, int ParticipantID, string ParticipantName,
    int CategoryID, string CategoryName,
    int EventID, string EventName, DateOnly EventDate,
    DateTime EnrolmentDate, string Status);

public record ResultDto(
    int ResultID, int EnrolmentID,
    int EventID, string EventName,
    int CategoryID, string CategoryName,
    int ParticipantID, string ParticipantName,
    TimeOnly? FinishTime, int? OverallPosition, int? CategoryPosition, string Status,
    DateOnly EventDate, int TotalFinishers);

// ----- request bodies sent to the API -----
public record RegisterPayload(string FullName, string Email, string Password, string Role, string? OrganiserCode);

public record ProfilePayload(string FullName, string Email);

public record EventPayload(string EventName, DateOnly EventDate, string Location, string EventType, decimal DistanceKm, string? Description);

public record CategoryPayload(string CategoryName, decimal? DistanceKm, decimal EntryFee, int MaxParticipants);

public record ResultPayload(TimeOnly? FinishTime, int? OverallPosition, int? CategoryPosition, string Status);
//ST10486532
//Author: Gift Tshwaane