using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Serves images to the browser. The pictures live in Azure Blob Storage, but the MVC app never talks to Azure:
/// it asks the API, and the API reads the blob.
/// </summary>
[Route("media")]
public class MediaController : Controller
{
    private readonly IRaceDayApi _api;

    public MediaController(IRaceDayApi api) => _api = api;

    [AllowAnonymous]
    [HttpGet("events/{id:int}")]
    public async Task<IActionResult> EventBanner(int id)
    {
        var file = await _api.GetBannerAsync(id);
        if (file == null) return NotFound();

        Response.Headers.CacheControl = "no-cache";
        return File(file.Bytes, file.ContentType);
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> ProfilePicture()
    {
        var file = await _api.GetProfilePictureAsync();
        if (file == null) return NotFound();

        Response.Headers.CacheControl = "private, no-cache";
        return File(file.Bytes, file.ContentType);
    }
}
//ST10486532
//Author: Gift Tshwaane