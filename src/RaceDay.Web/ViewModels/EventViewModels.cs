using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models;

namespace RaceDay.Web.ViewModels;

public class HomeIndexViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Search { get; set; }
    public string? Type { get; set; }
    public string? Location { get; set; }
    public string? LoadError { get; set; }
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = null!;

    /// <summary>The participant's current (confirmed) enrolment in this event, if any.</summary>
    public EnrolmentDto? MyEnrolment { get; set; }
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "The name must be between 3 and 150 characters.")]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose the event date.")]
    [Display(Name = "Date")]
    public DateOnly? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location.")]
    [StringLength(150, ErrorMessage = "The location is too long.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose the event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [StringLength(1000, ErrorMessage = "The description must be 1000 characters or fewer.")]
    public string? Description { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public bool HasBanner { get; set; }
}

public class OrganiserDashboardViewModel
{
    public List<OrganiserEventDto> Events { get; set; } = new();
    public int TotalEvents => Events.Count;
    public int UpcomingEvents { get; set; }
    public int TotalEnrolments => Events.Sum(e => e.ConfirmedEnrolments);
    public OrganiserEventDto? NextEvent { get; set; }
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Please name the category, e.g. 10km or Under 20.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The name must be between 2 and 100 characters.")]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.01, 1000, ErrorMessage = "The distance must be between 0.01 and 1000 km.")]
    [Display(Name = "Distance (km) - leave empty for an age group")]
    public decimal? DistanceKm { get; set; }

    [Range(0, 100000, ErrorMessage = "The entry fee must be 0 or more.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }

    [Range(1, 100000, ErrorMessage = "There must be room for at least 1 participant.")]
    [Display(Name = "Maximum participants")]
    public int MaxParticipants { get; set; } = 100;
}

public class CategoriesPageViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class EnrolmentsPageViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRowViewModel
{
    public EnrolmentDto Enrolment { get; set; } = null!;
    public ResultDto? Result { get; set; }
}

public class ResultsPageViewModel
{
    public EventDetailDto Event { get; set; } = null!;
    public bool EventHasHappened { get; set; }
    public List<ResultRowViewModel> Rows { get; set; } = new();
}

/// <summary>One row of the results entry page is submitted on its own.</summary>
public class SaveResultInput
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }
    public string? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = "Finished";
}
//ST10486532
//Author: Gift Tshwaane