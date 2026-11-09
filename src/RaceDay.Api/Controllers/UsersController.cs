using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>The logged-in user's own profile, enrolments and results.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayContext _db;
    private readonly IImageStorage _images;

    public UsersController(RaceDayContext db, IImageStorage images)
    {
        _db = db;
        _images = images;
    }

    /// <summary>Get the profile of the logged-in user.</summary>
    /// <remarks>Role required: any logged-in user (Organiser or Participant).</remarks>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == CurrentUserId);
        if (user == null) return Fail(StatusCodes.Status401Unauthorized, "Session no longer valid");
        return Ok(UserResponse.From(user));
    }

    /// <summary>Update the logged-in user's own name and email.</summary>
    /// <remarks>Role required: any logged-in user. Role cannot be changed here.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="409">Another account already uses that email.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> UpdateMe(UpdateProfileRequest req)
    {
        var userId = CurrentUserId;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == userId);
        if (user == null) return Fail(StatusCodes.Status401Unauthorized, "Session no longer valid");

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email && u.UserID != userId))
            return Fail(StatusCodes.Status409Conflict, "Email already in use", "Another account already uses this email.");

        user.FullName = req.FullName.Trim();
        user.Email = email;
        await _db.SaveChangesAsync();
        return Ok(UserResponse.From(user));
    }

    /// <summary>List the logged-in participant's own enrolments.</summary>
    /// <remarks>Role required: Participant.</remarks>
    /// <response code="200">All of the participant's enrolments, newest first.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("me/enrolments")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMyEnrolments()
    {
        var userId = CurrentUserId;
        var enrolments = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Where(e => e.ParticipantID == userId)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(enrolments.Select(EnrolmentResponse.From));
    }

    /// <summary>List the logged-in participant's own results.</summary>
    /// <remarks>Role required: Participant.</remarks>
    /// <response code="200">The participant's results history.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("me/results")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMyResults()
    {
        var userId = CurrentUserId;
        var results = await _db.Results.AsNoTracking()
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event)
            .Where(r => r.Enrolment.ParticipantID == userId)
            .ToListAsync();

        // How many people finished each of these events, so the UI can show "47th out of 312 finishers".
        var eventIds = results.Select(r => r.Enrolment.Category.EventID).Distinct().ToList();
        var finisherCounts = await _db.Results.AsNoTracking()
            .Where(r => eventIds.Contains(r.Enrolment.Category.EventID) && r.Status == ResultStatuses.Finished)
            .GroupBy(r => r.Enrolment.Category.EventID)
            .Select(g => new { EventID = g.Key, Count = g.Count() })
            .ToListAsync();
        var totals = finisherCounts.ToDictionary(x => x.EventID, x => x.Count);

        return Ok(results
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .Select(r => ResultResponse.From(r, totals.TryGetValue(r.Enrolment.Category.EventID, out var t) ? t : 0)));
    }

    /// <summary>Upload (or replace) the logged-in user's profile picture. The file is stored in Azure Blob Storage.</summary>
    /// <remarks>
    /// Role required: any logged-in user.
    ///
    /// Send multipart/form-data with a single file field called "file". JPG, PNG, GIF or WebP up to 5 MB.
    /// </remarks>
    /// <response code="200">The updated profile (hasProfilePicture is now true).</response>
    /// <response code="400">No file, file too large, or not a supported image.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("me/profile-picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageValidator.MaxRequestBytes)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> UploadProfilePicture(IFormFile file)
    {
        var userId = CurrentUserId;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == userId);
        if (user == null) return Fail(StatusCodes.Status401Unauthorized, "Session no longer valid");

        var (image, error) = await ImageValidator.ValidateAsync(file);
        if (image == null)
            return Fail(StatusCodes.Status400BadRequest, "Invalid image", error);

        await using (image.Content)
        {
            var blobName = $"profiles/{userId}/avatar-{Guid.NewGuid():N}{image.Extension}";
            await _images.SaveAsync(blobName, image.Content, image.ContentType);

            var previous = user.ProfilePictureBlobName;
            user.ProfilePictureBlobName = blobName;
            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(previous))
                await _images.DeleteIfExistsAsync(previous);
        }

        return Ok(UserResponse.From(user));
    }

    /// <summary>Download the logged-in user's own profile picture.</summary>
    /// <remarks>Role required: any logged-in user.</remarks>
    /// <response code="200">The image file.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No profile picture has been uploaded.</response>
    [HttpGet("me/profile-picture")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfilePicture()
    {
        var userId = CurrentUserId;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserID == userId);
        if (user == null) return Fail(StatusCodes.Status401Unauthorized, "Session no longer valid");
        if (string.IsNullOrEmpty(user.ProfilePictureBlobName))
            return Fail(StatusCodes.Status404NotFound, "No profile picture");

        var download = await _images.OpenReadAsync(user.ProfilePictureBlobName);
        if (download == null)
            return Fail(StatusCodes.Status404NotFound, "No profile picture");

        return File(download.Content, download.ContentType);
    }
}
//ST10486532
//Author: Gift Tshwaane