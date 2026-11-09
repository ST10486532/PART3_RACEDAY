using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Pages for logged-in Organisers: dashboard, events, categories, enrolments and results.</summary>
[Authorize(Roles = "Organiser")]
public class OrganiserController : WebControllerBase
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly IRaceDayApi _api;

    public OrganiserController(IRaceDayApi api) => _api = api;

    // ------------------------------------------------------------------
    // Dashboard and events list
    // ------------------------------------------------------------------

    public async Task<IActionResult> Dashboard()
    {
        var result = await _api.GetMyEventsAsync();
        if (!result.IsSuccess)
        {
            FlashError(result.Error ?? "We could not load your dashboard.");
            return View(new OrganiserDashboardViewModel());
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var events = result.Value!.OrderBy(e => e.EventDate).ToList();
        var upcoming = events.Where(e => e.EventDate >= today).ToList();

        return View(new OrganiserDashboardViewModel
        {
            Events = events,
            UpcomingEvents = upcoming.Count,
            NextEvent = upcoming.FirstOrDefault()
        });
    }

    public async Task<IActionResult> Events()
    {
        var result = await _api.GetMyEventsAsync();
        if (!result.IsSuccess)
        {
            FlashError(result.Error ?? "We could not load your events.");
            return View(new List<OrganiserEventDto>());
        }
        return View(result.Value!.OrderBy(e => e.EventDate).ToList());
    }

    // ------------------------------------------------------------------
    // Create / edit / delete event
    // ------------------------------------------------------------------

    [HttpGet]
    public IActionResult CreateEvent() =>
        View("EventForm", new EventFormViewModel { EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)) });

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        model.EventId = null;
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await _api.CreateEventAsync(ToPayload(model));
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We could not create the event.");
            return View("EventForm", model);
        }

        var eventId = result.Value!.EventID;
        if (model.Banner is { Length: > 0 })
        {
            var banner = await _api.UploadBannerAsync(eventId, model.Banner);
            if (!banner.IsSuccess)
            {
                FlashWarning($"The event was created, but the banner could not be uploaded: {banner.Error}");
                return RedirectToAction(nameof(Categories), new { id = eventId });
            }
        }

        FlashSuccess("Event created. Now add the categories people can enter.");
        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        return View("EventForm", new EventFormViewModel
        {
            EventId = ev.EventID,
            EventName = ev.EventName,
            EventDate = ev.EventDate,
            Location = ev.Location,
            EventType = ev.EventType,
            DistanceKm = ev.DistanceKm,
            Description = ev.Description,
            HasBanner = ev.HasBanner
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var result = await _api.UpdateEventAsync(id, ToPayload(model));
        if (!result.IsSuccess)
        {
            if (result.IsNotFound) return NotFound();
            ModelState.AddModelError(string.Empty, result.Error ?? "We could not save the event.");
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var banner = await _api.UploadBannerAsync(id, model.Banner);
            if (!banner.IsSuccess)
            {
                FlashWarning($"The event was saved, but the banner could not be uploaded: {banner.Error}");
                return RedirectToAction(nameof(Events));
            }
        }

        FlashSuccess("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await _api.DeleteEventAsync(id);
        if (result.IsSuccess)
            FlashSuccess("Event deleted.");
        else if (result.IsConflict)
            FlashError("This event already has enrolments, so it cannot be deleted.");
        else
            FlashError(result.Error ?? "We could not delete that event.");

        return RedirectToAction(nameof(Events));
    }

    // ------------------------------------------------------------------
    // Categories
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Categories(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        return View(new CategoriesPageViewModel { Event = ev, NewCategory = new CategoryFormViewModel { EventId = id } });
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(CategoryFormViewModel model)
    {
        var (ev, failure) = await LoadOwnedEventAsync(model.EventId);
        if (ev == null) return failure!;

        if (!ModelState.IsValid)
            return View("Categories", new CategoriesPageViewModel { Event = ev, NewCategory = model });

        var result = await _api.AddCategoryAsync(model.EventId, ToPayload(model));
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We could not add that category.");
            return View("Categories", new CategoriesPageViewModel { Event = ev, NewCategory = model });
        }

        FlashSuccess($"Category \"{result.Value!.CategoryName}\" added.");
        return RedirectToAction(nameof(Categories), new { id = model.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id, int eventId)
    {
        var (ev, failure) = await LoadOwnedEventAsync(eventId);
        if (ev == null) return failure!;

        var category = ev.Categories.FirstOrDefault(c => c.CategoryID == id);
        if (category == null) return NotFound();

        ViewData["EventName"] = ev.EventName;
        return View(new CategoryFormViewModel
        {
            EventId = eventId,
            CategoryId = id,
            CategoryName = category.CategoryName,
            DistanceKm = category.DistanceKm,
            EntryFee = category.EntryFee,
            MaxParticipants = category.MaxParticipants
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditCategory(CategoryFormViewModel model)
    {
        if (model.CategoryId is null) return NotFound();
        ViewData["EventName"] = "this event";
        if (!ModelState.IsValid) return View(model);

        var result = await _api.UpdateCategoryAsync(model.CategoryId.Value, ToPayload(model));
        if (!result.IsSuccess)
        {
            if (result.IsNotFound) return NotFound();
            ModelState.AddModelError(string.Empty, result.Error ?? "We could not save the category.");
            return View(model);
        }

        FlashSuccess("Category updated.");
        return RedirectToAction(nameof(Categories), new { id = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        var result = await _api.DeleteCategoryAsync(id);
        if (result.IsSuccess)
            FlashSuccess("Category removed.");
        else if (result.IsConflict)
            FlashError("People have already enrolled in this category, so it cannot be removed.");
        else
            FlashError(result.Error ?? "We could not remove that category.");

        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    // ------------------------------------------------------------------
    // Enrolments
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Enrolments(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        var enrolments = await _api.GetEventEnrolmentsAsync(id);
        if (!enrolments.IsSuccess)
        {
            FlashError(enrolments.Error ?? "We could not load the enrolments.");
            return View(new EnrolmentsPageViewModel { Event = ev });
        }

        return View(new EnrolmentsPageViewModel
        {
            Event = ev,
            Enrolments = enrolments.Value!.OrderBy(e => e.CategoryName).ThenBy(e => e.ParticipantName).ToList()
        });
    }

    // ------------------------------------------------------------------
    // Results entry
    // ------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        var (ev, failure) = await LoadOwnedEventAsync(id);
        if (ev == null) return failure!;

        var enrolments = await _api.GetEventEnrolmentsAsync(id);
        var results = await _api.GetEventResultsAsync(id);
        if (!enrolments.IsSuccess || !results.IsSuccess)
        {
            FlashError("We could not load the results for this event.");
            return View(new ResultsPageViewModel { Event = ev });
        }

        var resultsByEnrolment = results.Value!.ToDictionary(r => r.EnrolmentID);
        var rows = enrolments.Value!
            .Where(e => e.Status == "Confirmed")
            .OrderBy(e => e.CategoryName).ThenBy(e => e.ParticipantName)
            .Select(e => new ResultRowViewModel
            {
                Enrolment = e,
                Result = resultsByEnrolment.TryGetValue(e.EnrolmentID, out var r) ? r : null
            })
            .ToList();

        return View(new ResultsPageViewModel
        {
            Event = ev,
            EventHasHappened = ev.EventDate <= DateOnly.FromDateTime(DateTime.Today),
            Rows = rows
        });
    }

    /// <summary>Saves one participant's result. Saving makes it visible on their "My Results" page straight away.</summary>
    [HttpPost]
    public async Task<IActionResult> SaveResult(SaveResultInput input)
    {
        IActionResult Back() => RedirectToAction(nameof(Results), "Organiser", new { id = input.EventId }, $"row-{input.EnrolmentId}");

        if (input.Status is not ("Finished" or "DNF" or "DQ"))
        {
            FlashError("Please choose Finished, DNF or DQ.");
            return Back();
        }

        TimeOnly? finishTime = null;
        if (!string.IsNullOrWhiteSpace(input.FinishTime))
        {
            if (!TimeOnly.TryParseExact(input.FinishTime.Trim(), new[] { "HH:mm:ss", "H:mm:ss", "H:mm" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                FlashError("The finish time must look like 01:23:45 (hours:minutes:seconds).");
                return Back();
            }
            finishTime = parsed;
        }

        if (input.Status == "Finished")
        {
            if (finishTime is null)
            {
                FlashError("A finished result needs a finish time.");
                return Back();
            }
            if (input.OverallPosition is null || input.OverallPosition < 1)
            {
                FlashError("A finished result needs a finishing position (1 or higher).");
                return Back();
            }
        }

        var payload = new ResultPayload(finishTime, input.OverallPosition, input.CategoryPosition, input.Status);
        var result = input.ResultId.HasValue
            ? await _api.UpdateResultAsync(input.ResultId.Value, payload)
            : await _api.CaptureResultAsync(input.EnrolmentId, payload);

        if (result.IsSuccess)
            FlashSuccess($"Result saved and published for {result.Value!.ParticipantName}.");
        else
            FlashError(result.Error ?? "We could not save that result.");

        return Back();
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    /// <summary>Loads an event and makes sure it belongs to the logged-in organiser.</summary>
    private async Task<(EventDetailDto? Event, IActionResult? Failure)> LoadOwnedEventAsync(int eventId)
    {
        var result = await _api.GetEventAsync(eventId);
        if (result.IsNotFound) return (null, NotFound());
        if (!result.IsSuccess)
        {
            FlashError(result.Error ?? "We could not load that event.");
            return (null, RedirectToAction(nameof(Events)));
        }
        if (result.Value!.OrganiserID != CurrentUserId) return (null, Forbid());
        return (result.Value, null);
    }

    private void ValidateBanner(EventFormViewModel model)
    {
        if (model.Banner is not { Length: > 0 }) return;

        if (model.Banner.Length > MaxImageBytes)
            ModelState.AddModelError(nameof(model.Banner), MaxImageMessage);

        var extension = Path.GetExtension(model.Banner.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
            ModelState.AddModelError(nameof(model.Banner), "The banner must be a JPG, PNG, GIF or WebP image.");
    }

    private static EventPayload ToPayload(EventFormViewModel model) => new(
        model.EventName.Trim(), model.EventDate!.Value, model.Location.Trim(), model.EventType,
        model.DistanceKm!.Value, string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim());

    private static CategoryPayload ToPayload(CategoryFormViewModel model) => new(
        model.CategoryName.Trim(), model.DistanceKm, model.EntryFee, model.MaxParticipants);
}
//ST10486532
//Author: Gift Tshwaane