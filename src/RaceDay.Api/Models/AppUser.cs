namespace RaceDay.Api.Models;

/// <summary>Maps to the Users table. Organisers and Participants share this table, split by Role.</summary>
public class AppUser
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = RoleNames.Participant;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Name of the profile picture blob in Azure Blob Storage (null when none uploaded).</summary>
    public string? ProfilePictureBlobName { get; set; }

    public ICollection<RaceEvent> OrganisedEvents { get; set; } = new List<RaceEvent>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
//ST10486532
//Author: Gift Tshwaane