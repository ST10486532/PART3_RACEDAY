using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Turns problems talking to the API into friendly pages instead of crashes or raw error text:
/// expired session -> back to login, wrong role -> access denied, API down -> "service unavailable".
/// </summary>
public class ApiExceptionFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var http = context.HttpContext;

        switch (context.Exception)
        {
            case ApiUnauthorizedException:
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                http.Session.Clear();
                SetFlash(http, "Warning", "Your session has ended. Please log in again.");
                context.Result = new RedirectToActionResult("Login", "Account", null);
                context.ExceptionHandled = true;
                break;

            case ApiForbiddenException:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                context.ExceptionHandled = true;
                break;

            case ApiUnavailableException:
                context.Result = new RedirectToActionResult("ServiceUnavailable", "Home", null);
                context.ExceptionHandled = true;
                break;
        }
    }

    private static void SetFlash(HttpContext http, string key, string message)
    {
        var factory = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>();
        factory.GetTempData(http)[key] = message;
    }
}
//ST10486532
//Author: Gift Tshwaane