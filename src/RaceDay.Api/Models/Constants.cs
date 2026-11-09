namespace RaceDay.Api.Models;

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = { Organiser, Participant };
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
}

public static class EnrolmentStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public static class ResultStatuses
{
    public const string Finished = "Finished";
    public const string DNF = "DNF";
    public const string DQ = "DQ";
}
//ST10486532
//Author: Gift Tshwaane