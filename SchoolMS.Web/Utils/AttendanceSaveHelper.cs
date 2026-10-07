using SchoolMS.Services;
using SchoolMS.Web.Controllers;

namespace SchoolMS.Web.Utils;

/// <summary>
/// One save path for the web page and the mobile API so both validate and store attendance identically:
/// no future dates, only Present/Absent/Late, batch saves only touch that batch's students, and every
/// row is stamped with the student's own class/section/batch (not the filter the screen happened to use).
/// </summary>
public static class AttendanceSaveHelper
{
    private static readonly string[] ValidStatuses = { "Present", "Absent", "Late" };

    public record Row(int StudentId, string Status, string? Remarks, int? ClassId, int? SectionId, int? BatchId);

    /// <summary>
    /// Pure validation + row building (no database), so the rules can be tested without touching data.
    /// <paramref name="batchStudentIds"/> is non-null for an attendance-batch save and limits the rows to that batch.
    /// </summary>
    public static (string? error, List<Row> rows) Plan(AttSaveReq req, DateTime today,
        HashSet<int>? batchStudentIds, IReadOnlyDictionary<int, SchoolMS.Domain.Student> activeStudents)
    {
        if (req.Entries == null || req.Entries.Count == 0)
            return ("No students to save.", new());
        if (req.Date.Date > today.Date)
            return ("Attendance cannot be taken for a future date.", new());
        var bad = req.Entries.FirstOrDefault(e => !ValidStatuses.Contains(e.Status));
        if (bad != null)
            return ($"Invalid attendance status '{bad.Status}'. Use Present, Absent or Late.", new());

        var rows = new List<Row>();
        foreach (var en in req.Entries.GroupBy(e => e.StudentId).Select(g => g.Last()))
        {
            if (batchStudentIds != null && !batchStudentIds.Contains(en.StudentId)) continue;
            if (!activeStudents.TryGetValue(en.StudentId, out var stu)) continue;   // inactive / unknown student
            rows.Add(new Row(en.StudentId, en.Status, en.Remarks,
                stu.ClassId ?? req.ClassId, stu.SectionId ?? req.SectionId, stu.BatchId ?? req.BatchId));
        }
        if (rows.Count == 0)
            return ("None of the students could be saved (inactive or not in this batch).", new());
        return (null, rows);
    }

    public static (bool ok, string message) Save(AttSaveReq req, int userId, AttendanceService svc,
        StudentService studentSvc, AttendanceBatchService batchSvc)
    {
        HashSet<int>? allowed = null;
        if (req.AttBatchId.HasValue)
        {
            var batch = batchSvc.GetById(req.AttBatchId.Value);
            if (batch == null) return (false, "Attendance batch not found.");
            allowed = batch.StudentIds.ToHashSet();
        }

        var (students, _) = studentSvc.GetAll(1, 10000, null, null, null, null, null, "Active");
        var byId = students.GroupBy(s => s.StudentId).ToDictionary(g => g.Key, g => g.First());

        var (error, rows) = Plan(req, DateTime.Today, allowed, byId);
        if (error != null) return (false, error);

        TimeSpan? st = TimeSpan.TryParse(req.StartTime, out var s0) ? s0 : null;
        TimeSpan? et = TimeSpan.TryParse(req.EndTime, out var e0) ? e0 : null;
        // Stored exactly as picked from the dropdowns (some master names carry a trailing space) so the
        // web page's "selected" comparison keeps matching what was saved.
        var subject = string.IsNullOrWhiteSpace(req.Subject) ? null : req.Subject;
        var sirName = string.IsNullOrWhiteSpace(req.SirName) ? null : req.SirName;

        foreach (var r in rows)
            svc.Save(r.StudentId, req.Date, r.Status, r.ClassId, r.SectionId, r.BatchId,
                r.Remarks, userId, subject, sirName, st, et);

        return (true, $"Attendance saved for {rows.Count} students.");
    }
}

/// <summary>
/// Monthly attendance report shared by the web page and the API. sp_GetAttendanceReport only understands
/// class + month + year, so the Section / Batch filters (offered on both screens) are applied here —
/// otherwise picking "Morning" would silently still list the Evening students. Phones come from one
/// student query instead of one lookup per row.
/// </summary>
public static class AttendanceReportHelper
{
    public static (List<SchoolMS.Domain.AttendanceReport> report, Dictionary<int, (string father, string mother)> phones) Build(
        int? classId, int? sectionId, int? batchId, int? month, int? year,
        AttendanceService svc, StudentService studentSvc)
    {
        var report = svc.GetReport(classId, month, year);
        var (students, _) = studentSvc.GetAll(1, 10000, null, classId, sectionId, batchId, null, "Active");
        var byId = students.GroupBy(s => s.StudentId).ToDictionary(g => g.Key, g => g.First());

        if (sectionId.HasValue || batchId.HasValue)
            report = report.Where(r => byId.ContainsKey(r.StudentId)).ToList();

        var phones = report
            .Where(r => byId.ContainsKey(r.StudentId))
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => (byId[g.Key].FatherPhone ?? "", byId[g.Key].MotherPhone ?? ""));
        return (report, phones);
    }
}
