using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Pages for logged-in Participants: enrolling, their enrolments and their personal race history.</summary>
[Authorize(Roles = "Participant")]
public class ParticipantController : WebControllerBase
{
    private readonly IRaceDayApi _api;

    public ParticipantController(IRaceDayApi api) => _api = api;

    /// <summary>Enrol the logged-in participant in the chosen category. The API records the enrolment immediately.</summary>
    [HttpPost]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        var result = await _api.EnrolAsync(categoryId);
        if (!result.IsSuccess)
        {
            FlashError(result.IsNotFound
                ? "That category no longer exists."
                : result.Error ?? "We could not enrol you. Please try again.");
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        FlashSuccess($"You are in! You are enrolled in {result.Value!.EventName} ({result.Value.CategoryName}).");
        return RedirectToAction(nameof(MyEnrolments));
    }

    public async Task<IActionResult> MyEnrolments()
    {
        var enrolments = await _api.GetMyEnrolmentsAsync();
        if (!enrolments.IsSuccess)
        {
            FlashError(enrolments.Error ?? "We could not load your enrolments.");
            return View(new MyEnrolmentsViewModel());
        }

        var results = await _api.GetMyResultsAsync();
        var enrolmentsWithResults = results.IsSuccess
            ? results.Value!.Select(r => r.EnrolmentID).ToHashSet()
            : new HashSet<int>();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = enrolments.Value!
            .OrderByDescending(e => e.EventDate)
            .Select(e =>
            {
                var hasResult = enrolmentsWithResults.Contains(e.EnrolmentID);
                var raceStatus = hasResult ? "Result available"
                    : e.EventDate < today ? "Awaiting result"
                    : "Upcoming";
                return new MyEnrolmentRow
                {
                    Enrolment = e,
                    RaceStatus = raceStatus,
                    CanCancel = e.Status == "Confirmed" && !hasResult && e.EventDate >= today
                };
            })
            .ToList();

        return View(new MyEnrolmentsViewModel { Rows = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _api.CancelEnrolmentAsync(id);
        if (result.IsSuccess)
            FlashSuccess("Your enrolment has been cancelled.");
        else
            FlashError(result.Error ?? "We could not cancel that enrolment.");

        return RedirectToAction(nameof(MyEnrolments));
    }

    public async Task<IActionResult> MyResults()
    {
        var result = await _api.GetMyResultsAsync();
        if (!result.IsSuccess)
        {
            FlashError(result.Error ?? "We could not load your results.");
            return View(new MyResultsViewModel());
        }

        return View(new MyResultsViewModel
        {
            Results = result.Value!.OrderByDescending(r => r.EventDate).ToList()
        });
    }
}
//ST10486532
//Author: Gift Tshwaane