using RaceDay.Web.Models;

namespace RaceDay.Web.ViewModels;

public class MyEnrolmentRow
{
    public EnrolmentDto Enrolment { get; set; } = null!;

    /// <summary>Upcoming, Awaiting result or Result available.</summary>
    public string RaceStatus { get; set; } = "Upcoming";

    public bool CanCancel { get; set; }
}

public class MyEnrolmentsViewModel
{
    public List<MyEnrolmentRow> Rows { get; set; } = new();
}

public class MyResultsViewModel
{
    public List<ResultDto> Results { get; set; } = new();
}
//ST10486532
//Author: Gift Tshwaane