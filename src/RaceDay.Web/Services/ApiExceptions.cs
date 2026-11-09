namespace RaceDay.Web.Services;

/// <summary>The API said the session is missing or expired (HTTP 401).</summary>
public class ApiUnauthorizedException : Exception
{
    public ApiUnauthorizedException() : base("The API session is missing or has expired.") { }
}

/// <summary>The API refused because the user's role is not allowed to do this (HTTP 403).</summary>
public class ApiForbiddenException : Exception
{
    public ApiForbiddenException() : base("The API refused this request for the current role.") { }
}

/// <summary>The API could not be reached (down, wrong address, timeout).</summary>
public class ApiUnavailableException : Exception
{
    public ApiUnavailableException(Exception inner) : base("The RaceDay API could not be reached.", inner) { }
}
//ST10486532
//Author: Gift Tshwaane