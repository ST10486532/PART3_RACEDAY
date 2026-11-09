using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Controllers;

public abstract class WebControllerBase : Controller
{
    protected const string MaxImageMessage = "Please choose an image that is 5 MB or smaller.";
    protected const long MaxImageBytes = 5 * 1024 * 1024;

    protected int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>Green message shown at the top of the next page.</summary>
    protected void FlashSuccess(string message) => TempData["Success"] = message;

    /// <summary>Red message shown at the top of the next page.</summary>
    protected void FlashError(string message) => TempData["Error"] = message;

    /// <summary>Amber message shown at the top of the next page.</summary>
    protected void FlashWarning(string message) => TempData["Warning"] = message;

    protected IActionResult RedirectToRoleHome(string? role = null)
    {
        role ??= User.FindFirst(ClaimTypes.Role)?.Value;
        return role == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
    }
}
//ST10486532
//Author: Gift Tshwaane