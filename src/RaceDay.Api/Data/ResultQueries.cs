using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public static class ResultQueries
{
    /// <summary>Number of participants who finished an event (used for "47th out of 312 finishers").</summary>
    public static Task<int> CountFinishersAsync(this RaceDayContext db, int eventId) =>
        db.Results.CountAsync(r => r.Enrolment.Category.EventID == eventId && r.Status == ResultStatuses.Finished);
}
//ST10486532
//Author: Gift Tshwaane