namespace RaceDay.Api.Models;

/// <summary>Maps to the Categories table. Can be distance-based (10km) or age-based (Under 20, Senior).</summary>
public class Category
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal? DistanceKm { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxParticipants { get; set; } = 100;

    public RaceEvent Event { get; set; } = null!;
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
//ST10486532
//Author: Gift Tshwaane