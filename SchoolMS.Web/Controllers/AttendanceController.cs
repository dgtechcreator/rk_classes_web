using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;
using SchoolMS.Web.Utils;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class AttendanceController(AttendanceService svc, LookupService lookup,
    MastersService mastersSvc, FacultyService facultySvc, StudentService studentSvc, AttendanceBatchService batchSvc, BatchHolidayService holidays) : Controller
{
    [RequirePermission("attendance_entry")]
    public IActionResult Index(DateTime? date, int? classId, int? sectionId, int? batchId, int? attBatchId,
        string? subject, string? sirName, string? startTime, string? endTime, int session = 1)
    {
        session = Math.Clamp(session, 1, AttendanceSaveHelper.MaxSessions);
        var cr = HttpContext.CurrentCr();   // Class Representative: sees and saves only his own class
        var d = (date ?? DateTime.Today).Date;
        if (d > DateTime.Today) d = DateTime.Today;   // attendance can be reviewed/edited for past days, never future ones
        if (cr != null) d = CrScope.Clamp(d);

        // Every active student with this day's status (AttendanceId is set once marked) — the batch cards
        // and the entry table both read from this one list so their numbers always agree.
        var dayRecords = svc.GetForDate(d, null, null, null, session);
        if (cr != null) { var crIds = cr.StudentIds(studentSvc); dayRecords = dayRecords.Where(r => crIds.Contains(r.StudentId)).ToList(); }
        var attendanceBatches = batchSvc.GetAll();
        if (cr != null)
        {
            var mine = dayRecords.Select(r => r.StudentId).ToHashSet();
            attendanceBatches = attendanceBatches.Where(b => b.StudentIds.Any(mine.Contains)).ToList();
            classId = cr.ClassId; sectionId = cr.SectionId; batchId = cr.EffectiveBatchId;
        }
        var dayHolidays = holidays.ForDate(d);
        var summaries = batchSvc.BuildSummaries(attendanceBatches, dayRecords, dayHolidays);

        AttendanceBatch? selectedBatch = null;
        List<AttendanceRecord> records;
        if (attBatchId.HasValue)
        {
            selectedBatch = attendanceBatches.FirstOrDefault(b => b.BatchId == attBatchId.Value);
            records = selectedBatch == null ? new() : batchSvc.RecordsFor(selectedBatch, dayRecords);
        }
        else if (cr != null)
            records = dayRecords;   // a CR goes straight to his whole class
        else if (classId.HasValue || sectionId.HasValue || batchId.HasValue)
            records = svc.GetForDate(d, classId, sectionId, batchId, session);
        else
            records = new();

        if (selectedBatch != null && dayHolidays.ContainsKey(selectedBatch.BatchId)) records = new();   // holiday: no entry table, the page shows the holiday banner
        var first = records.FirstOrDefault(r => r.Subject != null || r.SirName != null);
        var (teachers, _) = facultySvc.GetAll(null, "Active", null, 1, 1000);

        var totalPresent = records.Count(r => r.AttendanceStatus == "Present");
        var totalAbsent = records.Count(r => r.AttendanceStatus == "Absent");

        ViewBag.AttendanceBatches = summaries;
        ViewBag.Holidays = dayHolidays;
        ViewBag.Session = session;
        ViewBag.Sessions = svc.GetSessionsForDate(d);   // lectures that already have attendance on this date
        ViewBag.SelectedAttBatch = selectedBatch;
        ViewBag.Cr = cr;
        ViewBag.CrEarliest = CrScope.EarliestDate;

        return View(new AttendanceVM {
            Records=records, Date=d, ClassId=classId, SectionId=sectionId, BatchId=batchId,
            Subject   = subject   ?? first?.Subject,
            SirName   = sirName   ?? first?.SirName,
            StartTime = startTime ?? first?.StartTime?.ToString(@"hh\:mm"),
            EndTime   = endTime   ?? first?.EndTime?.ToString(@"hh\:mm"),
            TotalPresent = totalPresent,
            TotalAbsent = totalAbsent,
            TotalMarked = records.Count(r => r.AttendanceId.HasValue),
            Classes=lookup.GetClasses(), Sections=lookup.GetSections(), Batches=lookup.GetBatches(),
            Subjects=mastersSvc.GetSubjects(),
            Teachers=teachers
        });
    }

    [HttpPost]
    [RequirePermission("attendance_entry")]
    public IActionResult Save([FromBody] AttSaveReq req)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        var (ok, message) = AttendanceSaveHelper.Save(req, uid, svc, studentSvc, batchSvc, HttpContext.CurrentCr(), holidays);
        return Json(new { success = ok, message });
    }

    public record HolidayReq(int AttBatchId, DateTime Date, string? Reason);

    /// <summary>"No class today" for one attendance batch: parents of its students are notified.</summary>
    [HttpPost]
    [RequirePermission("attendance_entry")]
    public IActionResult MarkHoliday([FromBody] HolidayReq req)
    {
        var name = HttpContext.Session.GetString("DisplayName") ?? HttpContext.Session.GetString("FullName") ?? "";
        var (ok, error, n) = holidays.Mark(req.AttBatchId, req.Date, req.Reason, HttpContext.Session.GetUserId() ?? 0, name);
        return Json(new { success = ok, message = ok ? (n > 0 ? $"Holiday marked. {n} parent login(s) were told there is no class." : "Holiday marked.") : error });
    }

    [HttpPost]
    [RequirePermission("attendance_entry")]
    public IActionResult CancelHoliday([FromBody] HolidayReq req)
    {
        var (ok, error, n) = holidays.Cancel(req.AttBatchId, req.Date);
        return Json(new { success = ok, message = ok ? (n > 0 ? $"Holiday cancelled. {n} parent login(s) were told the class is on." : "Holiday cancelled.") : error });
    }

    [RequirePermission("attendance_report")]
    public IActionResult Report(int? classId, int? sectionId, int? batchId, int? month, int? year)
    {
        var (report, studentPhones) = AttendanceReportHelper.Build(classId, sectionId, batchId,
            month ?? DateTime.Today.Month, year ?? DateTime.Today.Year, svc, studentSvc);
        ViewBag.Classes = lookup.GetClasses();
        ViewBag.Sections = lookup.GetSections();
        ViewBag.Batches = lookup.GetBatches();
        ViewBag.ClassId = classId;
        ViewBag.SectionId = sectionId;
        ViewBag.BatchId = batchId;
        ViewBag.Month   = month ?? DateTime.Today.Month;
        ViewBag.Year    = year  ?? DateTime.Today.Year;
        ViewBag.StudentPhones = studentPhones;

        return View(report);
    }

    [RequirePermission("attendance_grid")]
    public IActionResult DateGrid(int? classId, int? sectionId, int? batchId,
        DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var to   = toDate   ?? DateTime.Today;
        var (students, attData) = classId.HasValue
            ? svc.GetDateGrid(classId, sectionId, batchId, from, to)
            : (new List<AttendanceRecord>(), new List<DateAttendanceEntry>());

        var dates = new List<DateTime>();
        for (var d = from; d <= to; d = d.AddDays(1)) dates.Add(d);

        ViewBag.Classes   = lookup.GetClasses();
        ViewBag.Sections  = lookup.GetSections();
        ViewBag.Batches   = lookup.GetBatches();
        ViewBag.ClassId   = classId;
        ViewBag.SectionId = sectionId;
        ViewBag.BatchId   = batchId;
        ViewBag.FromDate  = from;
        ViewBag.ToDate    = to;
        ViewBag.Dates     = dates;
        ViewBag.AttData   = attData;
        return View(students);
    }

    [HttpGet]
    public IActionResult GetDateWiseAttendance(string date, int? classId, int? sectionId, int? batchId)
    {
        try
        {
            if (!DateTime.TryParse(date, out var selectedDate))
                return Json(new List<object>());

            var cr = HttpContext.CurrentCr();
            var crIds = cr?.StudentIds(studentSvc);
            return Json(AttendanceDateWiseHelper.Build(svc, selectedDate, classId, sectionId, batchId, crIds));
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetStudentContact(int studentId)
    {
        try
        {
            var student = studentSvc.GetById(studentId);
            var cr = HttpContext.CurrentCr();
            if (student == null || (cr != null && !cr.Matches(student)))
                return Json(new { fatherPhone = "", motherPhone = "", studentPhone = "" });

            return Json(new {
                fatherPhone = student.FatherPhone ?? "",
                motherPhone = student.MotherPhone ?? "",
                studentPhone = student.Phone ?? ""
            });
        }
        catch
        {
            return Json(new { fatherPhone = "", motherPhone = "", studentPhone = "" });
        }
    }

    [HttpGet]
    public IActionResult GetAddedByName(int studentId)
    {
        try
        {
            var history = svc.GetStudentAttendanceDetail(studentId);
            var lastRecord = history.OrderByDescending(x => x.AttendanceDate).FirstOrDefault();

            // For now, return empty since StudentAttendanceDetail doesn't have teacher info
            // This would need to be enhanced with a proper query that includes the teacher name
            return Json(new { name = "—" });
        }
        catch
        {
            return Json(new { name = "—" });
        }
    }
}

public class AttSaveReq {
    public DateTime Date{get;set;} public int? ClassId{get;set;} public int? SectionId{get;set;} public int? BatchId{get;set;}
    public int? AttBatchId{get;set;}
    /// <summary>Lecture of the day: 1 = first (default), 2 = second ...</summary>
    public int Session{get;set;}=1;
    public string? Subject{get;set;} public string? SirName{get;set;}
    public string? StartTime{get;set;} public string? EndTime{get;set;}
    public List<AttEntry> Entries{get;set;}=new();
}
public class AttEntry { public int StudentId{get;set;} public string Status{get;set;}="Present"; public string? Remarks{get;set;} }
