using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Edit or remove a category, and let participants enter an event by choosing a category.</summary>
[ApiController]
[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayContext _db;

    public CategoriesController(RaceDayContext db) => _db = db;

    /// <summary>Edit an existing category.</summary>
    /// <remarks>Role required: Organiser (owner of the category's event).</remarks>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own the event.</response>
    /// <response code="404">No category with that id.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Update(int id, CategoryRequest req)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return Fail(StatusCodes.Status404NotFound, "Category not found");
        if (category.Event.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only edit categories on events you created.");

        category.CategoryName = req.CategoryName.Trim();
        category.DistanceKm = req.DistanceKm;
        category.EntryFee = req.EntryFee;
        category.MaxParticipants = req.MaxParticipants;

        await _db.SaveChangesAsync();
        return Ok(CategoryResponse.From(category));
    }

    /// <summary>Remove a category from an event.</summary>
    /// <remarks>Role required: Organiser (owner of the category's event). Categories with enrolments cannot be removed.</remarks>
    /// <response code="200">Category removed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own the event.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">The category already has enrolments.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return Fail(StatusCodes.Status404NotFound, "Category not found");
        if (category.Event.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only remove categories from events you created.");
        if (await _db.Enrolments.AnyAsync(e => e.CategoryID == id))
            return Fail(StatusCodes.Status409Conflict, "Category has enrolments", "A category with enrolments cannot be removed.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return Ok(new MessageResponse("Category removed."));
    }

    /// <summary>Enter an event by enrolling the logged-in participant in a category.</summary>
    /// <remarks>
    /// Role required: Participant.
    ///
    /// The participant is taken from the session, so no request body is needed. The enrolment links the
    /// participant, the category and (through the category) the event. A participant may hold only one active
    /// enrolment per event, and a category stops accepting enrolments once MaxParticipants is reached.
    /// </remarks>
    /// <response code="201">The enrolment record.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">Already enrolled in this event, or the category is full.</response>
    [HttpPost("{id:int}/enrol")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(int id)
    {
        var userId = CurrentUserId;
        var participant = await _db.Users.FindAsync(userId);
        if (participant == null) return Fail(StatusCodes.Status401Unauthorized, "Session no longer valid");

        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return Fail(StatusCodes.Status404NotFound, "Category not found");

        var existing = await _db.Enrolments
            .Include(e => e.Category)
            .Where(e => e.ParticipantID == userId && e.Category.EventID == category.EventID)
            .ToListAsync();

        if (existing.Any(e => e.Status == EnrolmentStatuses.Confirmed))
            return Fail(StatusCodes.Status409Conflict, "Already enrolled", "You are already enrolled in this event.");

        var taken = await _db.Enrolments.CountAsync(e => e.CategoryID == id && e.Status == EnrolmentStatuses.Confirmed);
        if (taken >= category.MaxParticipants)
            return Fail(StatusCodes.Status409Conflict, "Category full", "This category has reached its maximum number of participants.");

        // A previously cancelled enrolment in the same category is re-activated (the table has a unique
        // constraint on participant + category).
        var enrolment = existing.FirstOrDefault(e => e.CategoryID == id);
        if (enrolment != null)
        {
            enrolment.Status = EnrolmentStatuses.Confirmed;
            enrolment.EnrolmentDate = DateTime.Now;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantID = userId,
                CategoryID = id,
                Participant = participant,
                Category = category
            };
            _db.Enrolments.Add(enrolment);
        }

        await _db.SaveChangesAsync();

        enrolment.Participant = participant;
        enrolment.Category = category;
        return Created("/api/users/me/enrolments", EnrolmentResponse.From(enrolment));
    }
}
//ST10486532
//Author: Gift Tshwaane