namespace SchoolMS.Domain;

/// <summary>
/// Academic years are NOT in chronological order by id (live: 2026-2027 = 5, 2025-2026 = 7, 2024-2025 = 8), so "earlier year" is
/// decided from the year's name: the first four digits ("2025-2026" -> 2025).
/// </summary>
public static class AcademicYearOrder
{
    public static int Start(string? yearName)
    {
        if (string.IsNullOrWhiteSpace(yearName)) return 0;
        var digits = new string(yearName.TrimStart().TakeWhile(char.IsDigit).ToArray());
        return digits.Length >= 4 && int.TryParse(digits[..4], out var y) ? y : 0;
    }

    /// <summary>True when [other] starts in an earlier year than [current] (unknown names are never "earlier").</summary>
    public static bool IsEarlier(string? other, string? current)
    {
        var a = Start(other);
        var b = Start(current);
        return a > 0 && b > 0 && a < b;
    }
}

/// <summary>Dues of earlier years typed in by staff for one student (the school started this system in 2026-27, so older dues were never in it).</summary>
public class PreviousDuesEntry
{
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string? UpdatedByName { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
