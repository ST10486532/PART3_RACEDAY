using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Register, log in, log out and the profile page. Authentication itself is done by the API.</summary>
public class AccountController : WebControllerBase
{
    private readonly IRaceDayApi _api;

    public AccountController(IRaceDayApi api) => _api = api;

    // ------------------------------------------------------------------
    // Login / logout
    // ------------------------------------------------------------------

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToRoleHome();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.LoginAsync(model.Email.Trim(), model.Password);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty,
                result.StatusCode == 401 ? "Incorrect email or password. Please try again." : result.Error ?? "We could not log you in.");
            model.Password = string.Empty;
            return View(model);
        }

        await StartSessionAsync(result.Value!);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToRoleHome(result.Value!.Role);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
            await _api.LogoutAsync();

        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        FlashSuccess("You have been logged out. See you at the next race!");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ------------------------------------------------------------------
    // Register
    // ------------------------------------------------------------------

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToRoleHome();
        return View(new RegisterViewModel());
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var isOrganiser = model.Role == "Organiser";
        if (model.Role != "Organiser" && model.Role != "Participant")
            ModelState.AddModelError(nameof(model.Role), "Please choose Participant or Organiser.");
        if (isOrganiser && string.IsNullOrWhiteSpace(model.OrganiserCode))
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need an invite code.");

        if (!ModelState.IsValid) return ClearPasswordsAndReturn(model);

        var result = await _api.RegisterAsync(new RegisterPayload(
            model.FullName.Trim(), model.Email.Trim(), model.Password, model.Role,
            isOrganiser ? model.OrganiserCode!.Trim() : null));

        if (!result.IsSuccess)
        {
            switch (result.StatusCode)
            {
                case 409:
                    ModelState.AddModelError(nameof(model.Email), "An account with this email already exists. Try logging in instead.");
                    break;
                case 403:
                    ModelState.AddModelError(nameof(model.OrganiserCode), "That organiser invite code is not valid.");
                    break;
                default:
                    ModelState.AddModelError(string.Empty, result.Error ?? "We could not create your account.");
                    break;
            }
            return ClearPasswordsAndReturn(model);
        }

        // Log the new user straight in.
        var login = await _api.LoginAsync(model.Email.Trim(), model.Password);
        if (login.IsSuccess)
        {
            await StartSessionAsync(login.Value!);
            FlashSuccess($"Welcome to RaceDay, {login.Value!.FullName}!");
            return RedirectToRoleHome(login.Value.Role);
        }

        FlashSuccess("Your account has been created. Please log in.");
        return RedirectToAction(nameof(Login));
    }

    // ------------------------------------------------------------------
    // Profile
    // ------------------------------------------------------------------

    [HttpGet, Authorize]
    public async Task<IActionResult> Profile()
    {
        var result = await _api.GetProfileAsync();
        if (!result.IsSuccess)
        {
            FlashError(result.Error ?? "We could not load your profile.");
            return RedirectToRoleHome();
        }
        return View(ToProfileModel(result.Value!));
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        // Role, join date and picture flag are display-only: reload them rather than trusting the form.
        var current = await _api.GetProfileAsync();
        if (current.IsSuccess)
        {
            model.Role = current.Value!.Role;
            model.MemberSince = current.Value.CreatedAt;
            model.HasProfilePicture = current.Value.HasProfilePicture;
        }

        if (!ModelState.IsValid) return View(model);

        var result = await _api.UpdateProfileAsync(new ProfilePayload(model.FullName.Trim(), model.Email.Trim()));
        if (!result.IsSuccess)
        {
            if (result.StatusCode == 409)
                ModelState.AddModelError(nameof(model.Email), "Another account already uses this email.");
            else
                ModelState.AddModelError(string.Empty, result.Error ?? "We could not save your changes.");
            return View(model);
        }

        // Keep the name in the session and sign-in cookie up to date.
        await StartSessionAsync(new LoginResultDto(result.Value!.UserID, result.Value.FullName, result.Value.Role, "Updated"), result.Value.HasProfilePicture);

        FlashSuccess("Your profile has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture == null || picture.Length == 0)
        {
            FlashError("Please choose a picture first.");
            return RedirectToAction(nameof(Profile));
        }
        if (picture.Length > MaxImageBytes)
        {
            FlashError(MaxImageMessage);
            return RedirectToAction(nameof(Profile));
        }

        var result = await _api.UploadProfilePictureAsync(picture);
        if (!result.IsSuccess)
        {
            FlashError(result.Error ?? "We could not upload that picture.");
            return RedirectToAction(nameof(Profile));
        }

        HttpContext.Session.SetString(SessionKeys.HasPicture, "1");
        FlashSuccess("Your profile picture has been updated.");
        return RedirectToAction(nameof(Profile));
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Remembers who is logged in: the name/role in the MVC session, and a signed-in cookie with a role claim
    /// so [Authorize(Roles = ...)] can protect the pages. (The API's own session cookie is already stored.)
    /// </summary>
    private async Task StartSessionAsync(LoginResultDto user, bool? hasPicture = null)
    {
        var session = HttpContext.Session;
        session.SetInt32(SessionKeys.UserId, user.UserID);
        session.SetString(SessionKeys.UserName, user.FullName);
        session.SetString(SessionKeys.Role, user.Role);

        if (hasPicture.HasValue)
        {
            session.SetString(SessionKeys.HasPicture, hasPicture.Value ? "1" : "0");
        }
        else
        {
            var profile = await _api.GetProfileAsync();
            session.SetString(SessionKeys.HasPicture, profile.IsSuccess && profile.Value!.HasProfilePicture ? "1" : "0");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }

    private IActionResult ClearPasswordsAndReturn(RegisterViewModel model)
    {
        model.Password = string.Empty;
        model.ConfirmPassword = string.Empty;
        ModelState.Remove(nameof(model.Password));
        ModelState.Remove(nameof(model.ConfirmPassword));
        return View(model);
    }

    private static ProfileViewModel ToProfileModel(UserDto user) => new()
    {
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role,
        MemberSince = user.CreatedAt,
        HasProfilePicture = user.HasProfilePicture
    };
}
//ST10486532
//Author: Gift Tshwaane