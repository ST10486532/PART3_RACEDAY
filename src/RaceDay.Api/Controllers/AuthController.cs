using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Login starts a cookie-based session that carries the user's role.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayContext _db;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly IConfiguration _config;

    public AuthController(RaceDayContext db, IPasswordHasher<AppUser> hasher, IConfiguration config)
    {
        _db = db;
        _hasher = hasher;
        _config = config;
    }

    /// <summary>Register a new account as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Role required: none (public).
    ///
    /// Passwords are hashed before storage. To stop anyone self-registering as an Organiser,
    /// an Organiser registration must include the invite code (organiserCode) issued by RaceDay.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user (never the password).</response>
    /// <response code="400">Validation failed (bad email, short password, unknown role).</response>
    /// <response code="403">Organiser invite code missing or incorrect.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest req)
    {
        var role = RoleNames.All.FirstOrDefault(r => string.Equals(r, req.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (role == null)
            return Fail(StatusCodes.Status400BadRequest, "Invalid role", "Role must be either 'Organiser' or 'Participant'.");

        if (role == RoleNames.Organiser)
        {
            var expected = _config["Registration:OrganiserCode"];
            if (string.IsNullOrWhiteSpace(expected) || req.OrganiserCode != expected)
                return Fail(StatusCodes.Status403Forbidden, "Invalid organiser code",
                    "Registering as an Organiser requires a valid organiser invite code.");
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Fail(StatusCodes.Status409Conflict, "Email already registered", "An account with this email already exists.");

        var user = new AppUser
        {
            FullName = req.FullName.Trim(),
            Email = email,
            Role = role
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created("/api/users/me", UserResponse.From(user));
    }

    /// <summary>Log in with email and password and start a session.</summary>
    /// <remarks>
    /// Role required: none (public).
    ///
    /// On success the server sets a secure, HTTP-only session cookie (RaceDay.Session) containing the
    /// user's id and role. The browser (and Swagger UI) sends it automatically on every later request.
    /// </remarks>
    /// <response code="200">Logged in. Returns userID, fullName and role; session cookie is set.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Same response for unknown email and wrong password so attackers cannot tell which accounts exist.
        if (user == null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
        {
            return Fail(StatusCodes.Status401Unauthorized, "Invalid credentials", "Email or password is incorrect.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });

        return Ok(new LoginResponse(user.UserID, user.FullName, user.Role, "Logged in."));
    }

    /// <summary>Log out and end the current session.</summary>
    /// <remarks>Role required: any logged-in user.</remarks>
    /// <response code="200">Session ended.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MessageResponse>> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new MessageResponse("Logged out."));
    }
}
//ST10486532
//Author: Gift Tshwaane