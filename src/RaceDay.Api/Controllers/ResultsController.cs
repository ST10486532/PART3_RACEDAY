using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Edit a previously captured race result.</summary>
[ApiController]
[Route("api/results")]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayContext _db;

    public ResultsController(RaceDayContext db) => _db = db;

    /// <summary>Edit a previously captured result.</summary>
    /// <remarks>Role required: Organiser (owner of the event).</remarks>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Participant, or an organiser who does not own the event.</response>
    /// <response code="404">No result with that id.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> Update(int id, ResultRequest req)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.ResultID == id);

        if (result == null) return Fail(StatusCodes.Status404NotFound, "Result not found");
        if (result.Enrolment.Category.Event.OrganiserID != CurrentUserId)
            return Fail(StatusCodes.Status403Forbidden, "Forbidden", "You can only edit results for events you created.");
        if (req.Status == ResultStatuses.Finished && req.FinishTime == null)
            return Fail(StatusCodes.Status400BadRequest, "Finish time required", "A Finished result must include a finishTime.");

        result.FinishTime = req.FinishTime;
        result.OverallPosition = req.OverallPosition;
        result.CategoryPosition = req.CategoryPosition;
        result.Status = req.Status;

        await _db.SaveChangesAsync();
        var totalFinishers = await _db.CountFinishersAsync(result.Enrolment.Category.EventID);
        return Ok(ResultResponse.From(result, totalFinishers));
    }
}
//ST10486532
//Author: Gift Tshwaane