using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Browse and manage events, plus the categories, enrolments and results that belong to an event.</summary>
[ApiController]
[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayContext _db;
    private readonly IImageStorage _images;

    public EventsController(RaceDayContext db, IImageStorage images)
    {
        _db = db;
        _images = images;
    }

    /// <summary>List the events created by the logged-in organiser, with enrolment totals (dashboard data).</summary>
    /// <remarks>Role required: Organiser.</remarks>
    /// <response code="200">The organiser's events, soonest first, each with its confirmed enrolment count.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpGet("mine")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<OrganiserEventSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<OrganiserEventSummary>>> GetMine()
    {
        var organiserId = CurrentUserId;
        var events = await _db.Events.AsNoTracking()
            .Include(e => e.Categories)
            .Where(e => e.OrganiserID == organiserId)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        var eventIds = events.Select(e => e.EventID).ToList();
        var counts = await _db.Enrolments.AsNoTracking()
            .Where(en => eventIds.Contains(en.Category.EventID) && en.Status == EnrolmentStatuses.Confirmed)
            .GroupBy(en => en.Category.EventID)
            .Select(g => new { EventID = g.Key, Count = g.Count() })
            .ToListAsync();
        var lookup = counts.ToDictionary(x => x.EventID, x => x.Count);

        return Ok(events.Select(e => OrganiserEventSummary.From(e, lookup.TryGetValue(e.EventID, out var c) ? c : 0)));
    }

    /// <summary>List events, soonest first. Optional filters: type (Run, Walk, Cycle) and location text.</summary>
    /// <remarks>Role required: none (public).</remarks>
    /// <param name="type">Optional event type filter: Run, Walk or Cycle.</param>
    /// <param name="location">Optional text the event location must contain.</param>
    /// <response code="200">Array of events (may be empty).</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetAll([FromQuery] string? type, [FromQuery] string? location)
    {
        IQueryable<RaceEvent> query = _db.Events.AsNoTracking().Include(e => e.Organiser);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(e => e.EventType == type);
        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(e => e.Location.Contains(location));

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(EventResponse.From));
    }

    /// <summary>Get one event with its categories and routes.</summary>
    /// <remarks>Role required: none (public).</remarks>
    /// <response code="200">The event, its categories and its routes.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .Include(e => e.Routes)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found");
        return Ok(EventDetailResponse.From(ev));
    }

    /// <summary>Create a new event. The logged-in organiser becomes its owner.</summary>
    /// <remarks>Role required: Organiser.</remarks>
    /// <response code="201">The new event.</response>
    /// <response code="400">Validation failed (missing name, bad type, etc.).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> Create(EventRequest req)
    {
        var organiserId = CurrentUserId;
        var organiser = await _db.Users.FindAsync(organiserId);
        if (organiser == null) return Fail(StatusCodes.Status401Unauthorized, "Session no longer valid");

        var ev = new RaceEvent
        {
            OrganiserID = organiserId,
            Organiser = organiser,
            EventName = req.EventName.Trim(),
            EventDate = req.EventDate!.Value,
            Location = req.Location.Trim(),
            EventType = req.EventType,
            DistanceKm = req.DistanceKm!.Value,
            Description = req.Description?.Trim()
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventID }, EventResponse.From(ev));
    }

    /// <summary>Edit an event. Only the organiser who created it may do this.</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> Update(int id, EventRequest req)
    {
        var ev = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only modify events you created.");

        ev.EventName = req.EventName.Trim();
        ev.EventDate = req.EventDate!.Value;
        ev.Location = req.Location.Trim();
        ev.EventType = req.EventType;
        ev.DistanceKm = req.DistanceKm!.Value;
        ev.Description = req.Description?.Trim();

        await _db.SaveChangesAsync();
        return Ok(EventResponse.From(ev));
    }

    /// <summary>Delete an event (and its categories and routes). Only the owning organiser may do this.</summary>
    /// <remarks>Role required: Organiser (owner of the event). Events that already have enrolments cannot be deleted.</remarks>
    /// <response code="200">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own this event.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="409">The event already has enrolments.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories)
            .Include(e => e.Routes)
            .FirstOrDefaultAsync(e => e.EventID == id);

        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only delete events you created.");

        var categoryIds = ev.Categories.Select(c => c.CategoryID).ToList();
        if (await _db.Enrolments.AnyAsync(e => categoryIds.Contains(e.CategoryID)))
            return Fail(StatusCodes.Status409Conflict, "Event has enrolments", "An event with enrolments cannot be deleted.");

        _db.Routes.RemoveRange(ev.Routes);
        _db.Categories.RemoveRange(ev.Categories);
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return Ok(new MessageResponse("Event deleted."));
    }

    // ------------------------------------------------------------------
    // Categories that belong to an event
    // ------------------------------------------------------------------

    /// <summary>List the categories (distance or age groups) available for an event.</summary>
    /// <remarks>Role required: none (public).</remarks>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{id:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetCategories(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == id))
            return Fail(StatusCodes.Status404NotFound, "Event not found");

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventID == id)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
        return Ok(categories.Select(CategoryResponse.From));
    }

    /// <summary>Add a category (e.g. 10km, 21km, Under 20, Senior) to an event.</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    /// <response code="201">The new category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPost("{id:int}/categories")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> AddCategory(int id, CategoryRequest req)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only add categories to events you created.");

        var category = new Category
        {
            EventID = id,
            CategoryName = req.CategoryName.Trim(),
            DistanceKm = req.DistanceKm,
            EntryFee = req.EntryFee,
            MaxParticipants = req.MaxParticipants
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{id}/categories", CategoryResponse.From(category));
    }

    // ------------------------------------------------------------------
    // Enrolments and results for an event
    // ------------------------------------------------------------------

    /// <summary>View every enrolment for an event (the race-day roster).</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    /// <response code="200">Array of enrolments with participant, category and event details.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{id:int}/enrolments")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetEnrolments(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only view enrolments for events you created.");

        var enrolments = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Where(e => e.Category.EventID == id)
            .OrderBy(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(enrolments.Select(EnrolmentResponse.From));
    }

    /// <summary>Get the results (leaderboard) for an event, best overall position first.</summary>
    /// <remarks>Role required: none (public).</remarks>
    /// <response code="200">Array of results ordered by overall position.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{id:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetResults(int id)
    {
        if (!await _db.Events.AnyAsync(e => e.EventID == id))
            return Fail(StatusCodes.Status404NotFound, "Event not found");

        var results = await _db.Results.AsNoTracking()
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event)
            .Where(r => r.Enrolment.Category.EventID == id)
            .ToListAsync();

        var totalFinishers = results.Count(r => r.Status == ResultStatuses.Finished);
        var ordered = results
            .OrderBy(r => r.OverallPosition ?? int.MaxValue)
            .ThenBy(r => r.FinishTime ?? TimeOnly.MaxValue)
            .Select(r => ResultResponse.From(r, totalFinishers));
        return Ok(ordered);
    }

    // ------------------------------------------------------------------
    // Event banner image (stored in Azure Blob Storage)
    // ------------------------------------------------------------------

    /// <summary>Upload (or replace) the banner image of an event. The file is stored in Azure Blob Storage.</summary>
    /// <remarks>
    /// Role required: Organiser (owner of the event).
    ///
    /// Send multipart/form-data with a single file field called "file". JPG, PNG, GIF or WebP up to 5 MB.
    /// The file type is verified from the file contents, not just the file name.
    /// </remarks>
    /// <response code="200">The updated event (hasBanner is now true).</response>
    /// <response code="400">No file, file too large, or not a supported image.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = RoleNames.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageValidator.MaxRequestBytes)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> UploadBanner(int id, IFormFile file)
    {
        var ev = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null) return Fail(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only change the banner of events you created.");

        var (image, error) = await ImageValidator.ValidateAsync(file);
        if (image == null)
            return Fail(StatusCodes.Status400BadRequest, "Invalid image", error);

        await using (image.Content)
        {
            var blobName = $"events/{id}/banner-{Guid.NewGuid():N}{image.Extension}";
            await _images.SaveAsync(blobName, image.Content, image.ContentType);

            var previous = ev.BannerBlobName;
            ev.BannerBlobName = blobName;
            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(previous))
                await _images.DeleteIfExistsAsync(previous);
        }

        return Ok(EventResponse.From(ev));
    }

    /// <summary>Download the banner image of an event.</summary>
    /// <remarks>Role required: none (public). The image is streamed from Azure Blob Storage through the API.</remarks>
    /// <response code="200">The image file.</response>
    /// <response code="404">The event does not exist or has no banner.</response>
    [HttpGet("{id:int}/banner")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBanner(int id)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventID == id);
        if (ev == null || string.IsNullOrEmpty(ev.BannerBlobName))
            return Fail(StatusCodes.Status404NotFound, "Banner not found");

        var download = await _images.OpenReadAsync(ev.BannerBlobName);
        if (download == null)
            return Fail(StatusCodes.Status404NotFound, "Banner not found");

        return File(download.Content, download.ContentType);
    }
}
//ST10486532
//Author: Gift Tshwaane