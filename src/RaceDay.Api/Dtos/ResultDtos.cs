using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public class ResultRequest
{
    /// <summary>Finish time (hh:mm:ss). Required when Status is Finished.</summary>
    /// <example>00:52:14</example>
    public TimeOnly? FinishTime { get; set; }

    /// <example>34</example>
    [Range(1, 1000000)]
    public int? OverallPosition { get; set; }

    /// <example>12</example>
    [Range(1, 1000000)]
    public int? CategoryPosition { get; set; }

    /// <summary>Finished, DNF (did not finish) or DQ (disqualified).</summary>
    /// <example>Finished</example>
    [Required, RegularExpression("^(Finished|DNF|DQ)$", ErrorMessage = "Status must be Finished, DNF or DQ.")]
    public string Status { get; set; } = ResultStatuses.Finished;
}

public record ResultResponse(
    int ResultID, int EnrolmentID,
    int EventID, string EventName,
    int CategoryID, string CategoryName,
    int ParticipantID, string ParticipantName,
    TimeOnly? FinishTime, int? OverallPosition, int? CategoryPosition, string Status,
    DateOnly EventDate, int TotalFinishers)
{
    /// <param name="totalFinishers">How many participants finished the event, for "47th out of 312 finishers".</param>
    public static ResultResponse From(RaceResult r, int totalFinishers) => new(
        r.ResultID, r.EnrolmentID,
        r.Enrolment.Category.EventID, r.Enrolment.Category.Event.EventName,
        r.Enrolment.CategoryID, r.Enrolment.Category.CategoryName,
        r.Enrolment.ParticipantID, r.Enrolment.Participant.FullName,
        r.FinishTime, r.OverallPosition, r.CategoryPosition, r.Status,
        r.Enrolment.Category.Event.EventDate, totalFinishers);
}
//ST10486532
//Author: Gift Tshwaane