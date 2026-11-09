using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class RegisterRequest
{
    /// <example>Thabo Mokoena</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <example>thabo.mokoena@raceday.co.za</example>
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <example>Passw0rd!</example>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    /// <example>Participant</example>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>Invite code issued by RaceDay. Required only when Role is Organiser.</summary>
    /// <example>RACEDAY-ORG-2026</example>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    /// <example>thabo.mokoena@raceday.co.za</example>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <example>Passw0rd!</example>
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    /// <example>Thabo Mokoena</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <example>thabo.mokoena@raceday.co.za</example>
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;
}

public record UserResponse(int UserID, string FullName, string Email, string Role, DateTime CreatedAt, bool HasProfilePicture)
{
    public static UserResponse From(AppUser u) =>
        new(u.UserID, u.FullName, u.Email, u.Role, u.CreatedAt, !string.IsNullOrEmpty(u.ProfilePictureBlobName));
}

public record LoginResponse(int UserID, string FullName, string Role, string Message);

public record MessageResponse(string Message);
//ST10486532
//Author: Gift Tshwaane