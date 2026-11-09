using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Public event details page (anyone can read it; only participants can enrol).</summary>
public class EventsController : WebControllerBase
{
    private readonly IRaceDayApi _api;

    public EventsController(IRaceDayApi api) => _api = api;

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetEventAsync(id);
        if (result.IsNotFound) return NotFound();
        if (!result.IsSuccess)
        {
            FlashError(result.Error ?? "We could not load that event.");
            return RedirectToAction("Index", "Home");
        }

        var model = new EventDetailsViewModel { Event = result.Value! };

        if (User.IsInRole("Participant"))
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            if (mine.IsSuccess)
            {
                model.MyEnrolment = mine.Value!.FirstOrDefault(e => e.EventID == id && e.Status == "Confirmed");
            }
        }

        return View(model);
    }
}
//ST10486532
//Author: Gift Tshwaane