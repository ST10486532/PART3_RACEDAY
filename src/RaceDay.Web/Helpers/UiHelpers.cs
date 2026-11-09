namespace RaceDay.Web.Helpers;

/// <summary>Small presentation helpers used by the Razor views (formatting only, no business rules).</summary>
public static class UiHelpers
{
    public static string StatusClass(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "confirmed" => "status-confirmed",     // green
        "pending" => "status-pending",         // amber
        "awaiting result" => "status-pending", // amber
        "cancelled" => "status-cancelled",     // red
        "finished" => "status-confirmed",      // green
        "result available" => "status-confirmed",
        "upcoming" => "status-upcoming",       // blue
        "dnf" => "status-pending",
        "dq" => "status-cancelled",
        _ => "status-upcoming"
    };

    public static string TypeIcon(string? eventType) => eventType switch
    {
        "Run" => "bi-lightning-charge-fill",
        "Walk" => "bi-person-walking",
        "Cycle" => "bi-bicycle",
        _ => "bi-flag-fill"
    };

    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13) return $"{number}th";
        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }

    public static string FormatTime(TimeOnly? time) => time.HasValue ? time.Value.ToString("HH:mm:ss") : "—";

    public static string FormatDate(DateOnly date) => date.ToString("ddd, d MMM yyyy");

    public static string Money(decimal amount) => amount <= 0 ? "Free" : $"R{amount:0.00}";

    public static string Distance(decimal? km) => km.HasValue ? $"{km.Value:0.##} km" : "Age group";

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }
}
//ST10486532
//Author: Gift Tshwaane