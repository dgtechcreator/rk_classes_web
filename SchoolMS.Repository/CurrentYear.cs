namespace SchoolMS.Repository;

/// <summary>
/// SQL fragment for "the academic year marked Current in Masters". Every count / list of students must use it, so that
/// switching the current year switches the whole app (same rule as sp_GetFinanceDashboardSummary).
/// Constant text only - never user input.
/// </summary>
public static class CurrentYear
{
    public const string Id = "(SELECT TOP 1 YearId FROM AcademicYears WHERE IsCurrent = 1 ORDER BY YearId DESC)";
}
