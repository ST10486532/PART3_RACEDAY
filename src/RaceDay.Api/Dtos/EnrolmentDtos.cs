using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Records the link between the Participant, the Event and the selected Category.</summary>
public record EnrolmentResponse(
    int EnrolmentID, int ParticipantID, string ParticipantName,
    int CategoryID, string CategoryName,
    int EventID, string EventName, DateOnly EventDate,
    DateTime EnrolmentDate, string Status)
{
    public static EnrolmentResponse From(Enrolment e) => new(
        e.EnrolmentID, e.ParticipantID, e.Participant.FullName,
        e.CategoryID, e.Category.CategoryName,
        e.Category.EventID, e.Category.Event.EventName, e.Category.Event.EventDate,
        e.EnrolmentDate, e.Status);
}
//ST10486532
//Author: Gift Tshwaane