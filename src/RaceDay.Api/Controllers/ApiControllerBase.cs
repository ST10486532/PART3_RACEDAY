using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The UserID stored in the session (cookie) of the logged-in user.</summary>
    protected int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    /// <summary>Returns an RFC 7807 problem response with the given status code.</summary>
    protected ObjectResult Fail(int status, string title, string? detail = null)
        => Problem(detail: detail, statusCode: status, title: title);
}
//ST10486532
//Author: Gift Tshwaane