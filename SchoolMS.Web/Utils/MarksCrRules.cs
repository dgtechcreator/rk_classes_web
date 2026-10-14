using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Controllers;

namespace SchoolMS.Web.Utils;

/// <summary>Rules for test marks typed by a Class Representative - shared by the web page and the mobile API.</summary>
public static class MarksCrRules
{
    /// <summary>Error text, or null when the save is allowed. Also forces the request to the CR's own class.</summary>
    public static string? CheckSave(CrScope cr, MarksSaveReq req, StudentService students)
    {
        if (req.Entries == null || req.Entries.Count == 0) return "No marks to save.";
        if (string.IsNullOrWhiteSpace(req.ExamName)) return "Enter the test name.";
        if (req.ExamName.Length > 100) return "Test name is too long.";
        if (req.MaxMarks < 1 || req.MaxMarks > 200) return "Out of marks must be between 1 and 200.";

        var date = (req.TestDate ?? DateTime.Today).Date;
        if (date > DateTime.Today) return "Test date cannot be in the future.";
        if (date < CrScope.EarliestMarksDate) return $"You can enter marks only for tests of the last {CrScope.MarksEditDays} days.";

        var mine = cr.StudentIds(students);
        if (req.Entries.Any(e => !mine.Contains(e.StudentId))) return "You can enter marks only for students of your own class.";
        var bad = req.Entries.FirstOrDefault(e => !e.IsAbsent && (e.Marks < 0 || e.Marks > req.MaxMarks));
        if (bad != null) return $"Marks must be between 0 and {req.MaxMarks}.";

        req.ClassId = cr.ClassId;
        return null;
    }
}
