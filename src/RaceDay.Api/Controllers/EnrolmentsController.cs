using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Cancel an enrolment (participant) or capture a race result for it (organiser).</summary>
[ApiController]
[Route("api/enrolments")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayContext _db;

    public EnrolmentsController(RaceDayContext db) => _db = db;

    /// <summary>Cancel the logged-in participant's own enrolment.</summary>
    /// <remarks>Role required: Participant (owner of the enrolment). The record is kept with status Cancelled.</remarks>
    /// <response code="200">Enrolment cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Organiser, or an enrolment that belongs to another participant.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">Already cancelled, or a result has already been captured.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MessageResponse>> Cancel(int id)
    {
        var enrolment = await _db.Enrolments.Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrolmentID == id);
        if (enrolment == null) return Fail(StatusCodes.Status404NotFound, "Enrolment not found");
        if (enrolment.ParticipantID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only cancel your own enrolments.");
        if (enrolment.Status == EnrolmentStatuses.Cancelled)
            return Fail(StatusCodes.Status409Conflict, "Already cancelled");
        if (enrolment.Result != null)
            return Fail(StatusCodes.Status409Conflict, "Result already captured", "An enrolment with a result cannot be cancelled.");

        enrolment.Status = EnrolmentStatuses.Cancelled;
        await _db.SaveChangesAsync();
        return Ok(new MessageResponse("Enrolment cancelled."));
    }

    /// <summary>Capture the finish time and positions for an enrolment.</summary>
    /// <remarks>Role required: Organiser (owner of the event). One result per enrolment.</remarks>
    /// <response code="201">The captured result.</response>
    /// <response code="400">Validation failed (e.g. Finished without a finish time).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own the event.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">A result already exists, or the enrolment was cancelled.</response>
    [HttpPost("{id:int}/result")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CaptureResult(int id, ResultRequest req)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentID == id);

        if (enrolment == null) return Fail(StatusCodes.Status404NotFound, "Enrolment not found");
        if (enrolment.Category.Event.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only capture results for events you created.");
        if (enrolment.Status != EnrolmentStatuses.Confirmed)
            return Fail(StatusCodes.Status409Conflict, "Enrolment cancelled", "Results cannot be captured for a cancelled enrolment.");
        if (enrolment.Result != null)
            return Fail(StatusCodes.Status409Conflict, "Result already captured", "Use PUT /api/results/{id} to edit it.");
        if (req.Status == ResultStatuses.Finished && req.FinishTime == null)
            return Fail(StatusCodes.Status400BadRequest, "Finish time required", "A Finished result must include a finishTime.");

        var result = new RaceResult
        {
            EnrolmentID = id,
            Enrolment = enrolment,
            FinishTime = req.FinishTime,
            OverallPosition = req.OverallPosition,
            CategoryPosition = req.CategoryPosition,
            Status = req.Status
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var totalFinishers = await _db.CountFinishersAsync(enrolment.Category.EventID);
        return Created($"/api/results/{result.ResultID}", ResultResponse.From(result, totalFinishers));
    }
}
//ST10486532
//Author: Gift Tshwaane