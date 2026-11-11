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

    /// <summary>Most lectures one class can have on a single day (holiday / Sunday batches usually have 2).</summary>
    public const int MaxSessions = 6;

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
        if (req.Session < 1 || req.Session > MaxSessions)
            return ($"Lecture number must be between 1 and {MaxSessions}.", new());
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
        StudentService studentSvc, AttendanceBatchService batchSvc, SchoolMS.Domain.CrScope? cr = null, BatchHolidayService? holidays = null)
    {
        // Class Representative: only his own class, and only today / the last few days.
        if (cr != null && req.Date.Date < CrScopeEarliest())
            return (false, $"You can mark attendance only for today and the last {SchoolMS.Domain.CrScope.EditDays} days.");

        HashSet<int>? allowed = null;
        if (req.AttBatchId.HasValue)
        {
            var batch = batchSvc.GetById(req.AttBatchId.Value);
            if (batch == null) return (false, "Attendance batch not found.");
            if (holidays != null && holidays.ForDate(req.Date).ContainsKey(batch.BatchId))
                return (false, "This batch is on holiday that day. Cancel the holiday first to take attendance.");
            allowed = batch.StudentIds.ToHashSet();
        }

        var (students, _) = studentSvc.GetAll(1, 10000, null, null, null, null, null, "Active");
        var byId = students.GroupBy(s => s.StudentId).ToDictionary(g => g.Key, g => g.First());
        if (cr != null) byId = byId.Where(kv => cr.Matches(kv.Value)).ToDictionary(kv => kv.Key, kv => kv.Value);

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
                r.Remarks, userId, subject, sirName, st, et, req.Session);

        return (true, req.Session > 1
            ? $"Attendance saved for {rows.Count} students (lecture {req.Session})."
            : $"Attendance saved for {rows.Count} students.");
    }

    static DateTime CrScopeEarliest() => SchoolMS.Domain.CrScope.EarliestDate;
}

/// <summary>
/// The "who was marked on this date" list shared by the web report and the API. A date can have several lectures
/// (a Sunday / holiday batch), so each student comes back once with a <c>sessions</c> list (lecture no. + status)
/// and a combined <c>status</c>: Present / Absent / Late when every lecture agrees, "Partial" when they differ
/// (for example came to the first lecture but not the second).
/// </summary>
public static class AttendanceDateWiseHelper
{
    public static List<object> Build(AttendanceService svc, DateTime date, int? classId, int? sectionId, int? batchId, HashSet<int>? crIds)
    {
        var lectures = svc.GetSessionsForDate(date).Select(s => s.Session).ToList();
        if (lectures.Count == 0) lectures.Add(1);

        var rows = new List<SchoolMS.Domain.AttendanceRecord>();
        foreach (var n in lectures)
            rows.AddRange(svc.GetForDate(date, classId, sectionId, batchId, n)
                .Where(r => r.AttendanceId.HasValue && (crIds == null || crIds.Contains(r.StudentId))));

        return rows.GroupBy(r => r.StudentId).Select(g =>
        {
            var ordered = g.OrderBy(r => r.SessionNo).ToList();
            var first = ordered[0];
            var statuses = ordered.Select(r => r.AttendanceStatus ?? "Present").Distinct().ToList();
            var combined = statuses.Count == 1 ? statuses[0] : "Partial";
            return (object)new {
                studentId = first.StudentId,
                studentName = first.FullName ?? "",
                admissionNo = first.AdmissionNo ?? "",
                className = first.ClassName ?? "",
                sectionName = first.SectionName ?? "",
                batchName = first.BatchName ?? "",
                status = combined,
                // "Present" for one lecture; "L1 Present · L2 Absent" when the day had several
                statusText = ordered.Count == 1 ? combined : string.Join(" · ", ordered.Select(r => $"L{r.SessionNo} {r.AttendanceStatus}")),
                sessions = ordered.Select(r => new { session = r.SessionNo, status = r.AttendanceStatus, subject = r.Subject ?? "", teacher = r.SirName ?? "" }),
                subject = string.Join(" / ", ordered.Select(r => r.Subject).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                teacher = string.Join(" / ", ordered.Select(r => r.SirName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
            };
        }).ToList();
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
