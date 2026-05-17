using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class AttendanceController(AttendanceService svc, LookupService lookup) : Controller
{
    public IActionResult Index(DateTime? date, int? classId, int? sectionId, int? batchId)
    {
        var d = date ?? DateTime.Today;
        var records = classId.HasValue
            ? svc.GetForDate(d, classId, sectionId, batchId)
            : new List<AttendanceRecord>();
        return View(new AttendanceVM {
            Records=records, Date=d, ClassId=classId, SectionId=sectionId, BatchId=batchId,
            Classes=lookup.GetClasses(), Sections=lookup.GetSections(), Batches=lookup.GetBatches()
        });
    }

    [HttpPost]
    public IActionResult Save([FromBody] AttSaveReq req)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        foreach (var e in req.Entries)
            svc.Save(e.StudentId, req.Date, e.Status, req.ClassId, req.SectionId, req.BatchId, e.Remarks, uid);
        return Json(new { success = true, message = $"Attendance saved for {req.Entries.Count} students." });
    }

    public IActionResult Report(int? classId, int? month, int? year)
    {
        var report = svc.GetReport(classId, month ?? DateTime.Today.Month, year ?? DateTime.Today.Year);
        ViewBag.Classes = lookup.GetClasses();
        ViewBag.ClassId = classId;
        ViewBag.Month   = month ?? DateTime.Today.Month;
        ViewBag.Year    = year  ?? DateTime.Today.Year;
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
}

public class AttSaveReq { public DateTime Date{get;set;} public int? ClassId{get;set;} public int? SectionId{get;set;} public int? BatchId{get;set;} public List<AttEntry> Entries{get;set;}=new(); }
public class AttEntry   { public int StudentId{get;set;} public string Status{get;set;}="Present"; public string? Remarks{get;set;} }
