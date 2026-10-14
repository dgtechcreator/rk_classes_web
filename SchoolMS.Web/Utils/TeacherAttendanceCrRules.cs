using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Utils;

/// <summary>
/// Rules for teacher-attendance entries typed by a Class Representative - one place so the web page and the mobile API
/// can never differ. The class / batch are forced to his own; he picks only the teacher, subject, topic, date and time.
/// </summary>
public static class TeacherAttendanceCrRules
{
    public const int MaxSubject = 100;
    public const int MaxTopic = 500;

    /// <summary>Returns an error message, or null when the entry is allowed. Class / batch are overwritten with the CR's own.</summary>
    public static string? Apply(CrScope cr, FacultyService facultySvc, int facultyId, DateTime date,
        ref int? classId, ref int? batchId)
    {
        classId = cr.ClassId;
        batchId = cr.EffectiveBatchId;
        if (date.Date > DateTime.Today) return "Attendance cannot be added for a future date.";
        if (date.Date < CrScope.EarliestDate) return $"You can add teacher attendance only for today and the last {CrScope.EditDays} days.";
        if (!facultySvc.GetAllActive().Any(f => f.FacultyId == facultyId)) return "Please select a valid teacher.";
        return null;
    }

    /// <summary>Length / time sanity checks used for every login (a too long topic used to fail with a raw SQL error).</summary>
    public static string? Validate(string? subject, string? topic, TimeSpan? inTime, TimeSpan? outTime)
    {
        if ((subject?.Length ?? 0) > MaxSubject) return $"Subject is too long (max {MaxSubject} characters).";
        if ((topic?.Length ?? 0) > MaxTopic) return $"Topic is too long (max {MaxTopic} characters).";
        if (inTime.HasValue && outTime.HasValue && outTime.Value <= inTime.Value) return "Out time must be after in time.";
        return null;
    }
}
