namespace RaceDay.Api.Models;

/// <summary>Maps to the Events table.</summary>
public class RaceEvent
{
    public int EventID { get; set; }
    public int OrganiserID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventType { get; set; } = EventTypes.Run;
    public decimal DistanceKm { get; set; }
    public string? Description { get; set; }

    /// <summary>Name of the banner image blob in Azure Blob Storage (null when no banner has been uploaded).</summary>
    public string? BannerBlobName { get; set; }

    public AppUser Organiser { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<RaceRoute> Routes { get; set; } = new List<RaceRoute>();
}
//ST10486532
//Author: Gift Tshwaane