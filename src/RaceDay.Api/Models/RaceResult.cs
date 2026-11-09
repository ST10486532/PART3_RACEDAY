namespace RaceDay.Api.Models;

/// <summary>Maps to the Results table: one result per enrolment.</summary>
public class RaceResult
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public TimeOnly? FinishTime { get; set; }
    public int? OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    public string Status { get; set; } = ResultStatuses.Finished;

    public Enrolment Enrolment { get; set; } = null!;
}
//ST10486532
//Author: Gift Tshwaane