namespace RaceDay.Api.Models;

/// <summary>Maps to the Enrolments table: links a Participant to a Category (and so to an Event).</summary>
public class Enrolment
{
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public int CategoryID { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public string Status { get; set; } = EnrolmentStatuses.Confirmed;

    public AppUser Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public RaceResult? Result { get; set; }
}
//ST10486532
//Author: Gift Tshwaane