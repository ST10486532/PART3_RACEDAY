using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

public class HomeController : WebControllerBase
{
    private readonly IRaceDayApi _api;

    public HomeController(IRaceDayApi api) => _api = api;

    /// <summary>Home page: upcoming events with filters. Organisers are sent to their dashboard instead.</summary>
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? search, string? type, string? location)
    {
        if (User.IsInRole("Organiser")) return RedirectToAction("Dashboard", "Organiser");

        var model = new HomeIndexViewModel { Search = search, Type = type, Location = location };

        var result = await _api.GetEventsAsync(type, location);
        if (!result.IsSuccess)
        {
            model.LoadError = "We could not load the events right now. Please try again in a moment.";
            return View(model);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var events = result.Value!.Where(e => e.EventDate >= today);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            events = events.Where(e =>
                e.EventName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (e.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        model.Events = events.OrderBy(e => e.EventDate).ThenBy(e => e.EventName).ToList();
        return View(model);
    }

    [AllowAnonymous]
    public IActionResult ServiceUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View();
    }

    [AllowAnonymous, IgnoreAntiforgeryToken]
    public IActionResult Error() => View();

    [AllowAnonymous, IgnoreAntiforgeryToken]
    public IActionResult HttpStatus(int code)
    {
        ViewData["Code"] = code;
        Response.StatusCode = code;
        return View();
    }
}
//ST10486532
//Author: Gift Tshwaane