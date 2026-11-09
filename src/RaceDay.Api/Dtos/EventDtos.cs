using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class EventRequest
{
    /// <example>Johannesburg City Run</example>
    [Required, StringLength(150, MinimumLength = 3)]
    public string EventName { get; set; } = string.Empty;

    /// <summary>Date of the event (yyyy-MM-dd).</summary>
    /// <example>2026-11-08</example>
    [Required]
    public DateOnly? EventDate { get; set; }

    /// <example>Sandton, Johannesburg</example>
    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Run, Walk or Cycle.</summary>
    /// <example>Run</example>
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk or Cycle.")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Headline distance of the event in kilometres.</summary>
    /// <example>10</example>
    [Required, Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }

    /// <example>Annual road run through the Sandton CBD.</example>
    [StringLength(1000)]
    public string? Description { get; set; }
}

public record EventResponse(
    int EventID, int OrganiserID, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string EventType, decimal DistanceKm, string? Description, bool HasBanner)
{
    public static EventResponse From(RaceEvent e) => new(
        e.EventID, e.OrganiserID, e.Organiser?.FullName ?? string.Empty, e.EventName, e.EventDate,
        e.Location, e.EventType, e.DistanceKm, e.Description, !string.IsNullOrEmpty(e.BannerBlobName));
}

/// <summary>One row of the organiser dashboard: an event they created plus its enrolment total.</summary>
public record OrganiserEventSummary(
    int EventID, string EventName, DateOnly EventDate, string Location, string EventType,
    decimal DistanceKm, string? Description, bool HasBanner, int CategoryCount, int ConfirmedEnrolments)
{
    public static OrganiserEventSummary From(RaceEvent e, int confirmedEnrolments) => new(
        e.EventID, e.EventName, e.EventDate, e.Location, e.EventType, e.DistanceKm, e.Description,
        !string.IsNullOrEmpty(e.BannerBlobName), e.Categories.Count, confirmedEnrolments);
}

public record RouteResponse(int RouteID, int EventID, string RouteName, decimal DistanceKm, decimal? ElevationGainM, string? MapUrl)
{
    public static RouteResponse From(RaceRoute r) => new(r.RouteID, r.EventID, r.RouteName, r.DistanceKm, r.ElevationGainM, r.MapUrl);
}

public record EventDetailResponse(
    int EventID, int OrganiserID, string OrganiserName, string EventName, DateOnly EventDate,
    string Location, string EventType, decimal DistanceKm, string? Description, bool HasBanner,
    IEnumerable<CategoryResponse> Categories, IEnumerable<RouteResponse> Routes)
{
    public static EventDetailResponse From(RaceEvent e) => new(
        e.EventID, e.OrganiserID, e.Organiser?.FullName ?? string.Empty, e.EventName, e.EventDate,
        e.Location, e.EventType, e.DistanceKm, e.Description, !string.IsNullOrEmpty(e.BannerBlobName),
        e.Categories.OrderBy(c => c.CategoryName).Select(CategoryResponse.From).ToList(),
        e.Routes.OrderBy(r => r.RouteName).Select(RouteResponse.From).ToList());
}
//ST10486532
//Author: Gift Tshwaane