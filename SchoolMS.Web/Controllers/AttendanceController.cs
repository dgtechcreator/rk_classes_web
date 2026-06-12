using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class AttendanceController(AttendanceService svc, LookupService lookup,
    MastersService mastersSvc, FacultyService facultySvc, StudentService studentSvc) : Controller
{
    public IActionResult Index(DateTime? date, int? classId, int? sectionId, int? batchId,
        string? subject, string? sirName, string? startTime, string? endTime)
    {
        var d = date ?? DateTime.Today;
        var records = classId.HasValue
            ? svc.GetForDate(d, classId, sectionId, batchId)
            : new List<AttendanceRecord>();
        var first = records.FirstOrDefault(r => r.Subject != null || r.SirName != null);
        var (teachers, _) = facultySvc.GetAll(null, "Active", null, 1, 1000);
        return View(new AttendanceVM {
            Records=records, Date=d, ClassId=classId, SectionId=sectionId, BatchId=batchId,
            Subject   = subject   ?? first?.Subject,
            SirName   = sirName   ?? first?.SirName,
            StartTime = startTime ?? first?.StartTime?.ToString(@"hh\:mm"),
            EndTime   = endTime   ?? first?.EndTime?.ToString(@"hh\:mm"),
            Classes=lookup.GetClasses(), Sections=lookup.GetSections(), Batches=lookup.GetBatches(),
            Subjects=mastersSvc.GetSubjects(),
            Teachers=teachers
        });
    }

    [HttpPost]
    public IActionResult Save([FromBody] AttSaveReq req)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        TimeSpan? st = TimeSpan.TryParse(req.StartTime, out var s) ? s : null;
        TimeSpan? et = TimeSpan.TryParse(req.EndTime,   out var e) ? e : null;
        foreach (var en in req.Entries)
            svc.Save(en.StudentId, req.Date, en.Status, req.ClassId, req.SectionId, req.BatchId,
                en.Remarks, uid, req.Subject, req.SirName, st, et);
        return Json(new { success = true, message = $"Attendance saved for {req.Entries.Count} students." });
    }

    public IActionResult Report(int? classId, int? sectionId, int? batchId, int? month, int? year)
    {
        var report = svc.GetReport(classId, month ?? DateTime.Today.Month, year ?? DateTime.Today.Year);
        ViewBag.Classes = lookup.GetClasses();
        ViewBag.Sections = lookup.GetSections();
        ViewBag.Batches = lookup.GetBatches();
        ViewBag.ClassId = classId;
        ViewBag.SectionId = sectionId;
        ViewBag.BatchId = batchId;
        ViewBag.Month   = month ?? DateTime.Today.Month;
        ViewBag.Year    = year  ?? DateTime.Today.Year;

        // Get student phone numbers for all students in report
        var studentPhones = new Dictionary<int, (string father, string mother)>();
        foreach (var r in report)
        {
            var student = studentSvc.GetById(r.StudentId);
            if (student != null)
            {
                studentPhones[r.StudentId] = (student.FatherPhone ?? "", student.MotherPhone ?? "");
            }
        }
        ViewBag.StudentPhones = studentPhones;

        return View(report);
    }

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

            var records = svc.GetForDate(selectedDate, classId, sectionId, batchId);

            var result = records
      .GroupBy(r => r.StudentId)
      .Select(g => new {
          studentId = g.First().StudentId,
          studentName = g.First().FullName ?? "",
          admissionNo = g.First().AdmissionNo ?? "",
          className = g.First().ClassName ?? "",
          sectionName = g.First().SectionName ?? "",
          batchName = g.First().BatchName ?? "",
          status = g.First().AttendanceStatus ?? "Present",
          subject = g.First().Subject ?? "",
          teacher = g.First().SirName ?? ""
      })
      .ToList();

            return Json(result);
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
            if (student == null)
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
    public string? Subject{get;set;} public string? SirName{get;set;}
    public string? StartTime{get;set;} public string? EndTime{get;set;}
    public List<AttEntry> Entries{get;set;}=new();
}
public class AttEntry { public int StudentId{get;set;} public string Status{get;set;}="Present"; public string? Remarks{get;set;} }
