namespace RaceDay.Api.Models;

/// <summary>Maps to the Routes table.</summary>
public class RaceRoute
{
    public int RouteID { get; set; }
    public int EventID { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal? ElevationGainM { get; set; }
    public string? MapUrl { get; set; }

    public RaceEvent Event { get; set; } = null!;
}
//ST10486532
//Author: Gift Tshwaane