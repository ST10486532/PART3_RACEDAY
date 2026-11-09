namespace RaceDay.Web.Services;

/// <summary>Keys used with the ASP.NET Core session (server-side) to remember who is logged in.</summary>
public static class SessionKeys
{
    /// <summary>The API's own session cookie (RaceDay.Session=...), forwarded on every API call.</summary>
    public const string ApiCookie = "ApiCookie";

    public const string UserId = "UserId";
    public const string UserName = "UserName";
    public const string Role = "Role";
    public const string HasPicture = "HasPicture";
}
//ST10486532
//Author: Gift Tshwaane