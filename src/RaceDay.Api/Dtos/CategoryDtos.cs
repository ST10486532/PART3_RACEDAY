using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class CategoryRequest
{
    /// <summary>Distance or age group, e.g. "10km", "21km", "Under 20", "Senior".</summary>
    /// <example>10km Challenge</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Distance in km. Leave empty for age-based categories such as "Under 20".</summary>
    /// <example>10</example>
    [Range(0.01, 1000)]
    public decimal? DistanceKm { get; set; }

    /// <example>150</example>
    [Range(0, 100000)]
    public decimal EntryFee { get; set; }

    /// <example>300</example>
    [Range(1, 100000)]
    public int MaxParticipants { get; set; } = 100;
}

public record CategoryResponse(int CategoryID, int EventID, string CategoryName, decimal? DistanceKm, decimal EntryFee, int MaxParticipants)
{
    public static CategoryResponse From(Category c) => new(c.CategoryID, c.EventID, c.CategoryName, c.DistanceKm, c.EntryFee, c.MaxParticipants);
}
//ST10486532
//Author: Gift Tshwaane