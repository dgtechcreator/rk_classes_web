using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Controllers;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/attendance")]
[ApiRequireStaff]
public class AttendanceApiController(AttendanceService svc, StudentService studentSvc) : ControllerBase
{
    [HttpGet]
    [ApiRequirePermission("attendance_entry")]
    public IActionResult Index(DateTime? date, int? classId, int? sectionId, int? batchId)
    {
        var d = date ?? DateTime.Today;
        var records = classId.HasValue
            ? svc.GetForDate(d, classId, sectionId, batchId)
            : svc.GetForDate(d, null, null, null).Where(r => r.AttendanceId.HasValue).ToList();

        var first = records.FirstOrDefault(r => r.Subject != null || r.SirName != null);
        var totalPresent = records.Count(r => r.AttendanceStatus == "Present");
        var totalAbsent = records.Count(r => r.AttendanceStatus == "Absent");

        return Ok(new {
            date = d,
            records,
            subject = first?.Subject,
            sirName = first?.SirName,
            startTime = first?.StartTime?.ToString(@"hh\:mm"),
            endTime = first?.EndTime?.ToString(@"hh\:mm"),
            totalPresent,
            totalAbsent,
            totalMarked = records.Count,
        });
    }

    [HttpPost("save")]
    [ApiRequirePermission("attendance_entry")]
    public IActionResult Save([FromBody] AttSaveReq req)
    {
        int uid = User.UserId() ?? 1;
        TimeSpan? st = TimeSpan.TryParse(req.StartTime, out var s) ? s : null;
        TimeSpan? et = TimeSpan.TryParse(req.EndTime, out var e) ? e : null;
        foreach (var en in req.Entries)
            svc.Save(en.StudentId, req.Date, en.Status, req.ClassId, req.SectionId, req.BatchId,
                en.Remarks, uid, req.Subject, req.SirName, st, et);
        return Ok(new { success = true, message = $"Attendance saved for {req.Entries.Count} students." });
    }

    [HttpGet("report")]
    [ApiRequirePermission("attendance_report")]
    public IActionResult Report(int? classId, int? sectionId, int? batchId, int? month, int? year)
    {
        var report = svc.GetReport(classId, month ?? DateTime.Today.Month, year ?? DateTime.Today.Year);

        var studentPhones = report
            .Select(r => r.StudentId)
            .Distinct()
            .Select(id => studentSvc.GetById(id))
            .Where(s => s != null)
            .ToDictionary(s => s!.StudentId, s => new { father = s!.FatherPhone ?? "", mother = s!.MotherPhone ?? "" });

        return Ok(new { report, studentPhones });
    }

    [HttpGet("date-grid")]
    [ApiRequirePermission("attendance_grid")]
    public IActionResult DateGrid(int? classId, int? sectionId, int? batchId, DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var to = toDate ?? DateTime.Today;
        var (students, attData) = classId.HasValue
            ? svc.GetDateGrid(classId, sectionId, batchId, from, to)
            : (new List<AttendanceRecord>(), new List<DateAttendanceEntry>());

        var dates = new List<DateTime>();
        for (var d = from; d <= to; d = d.AddDays(1)) dates.Add(d);

        return Ok(new { students, attData, dates, fromDate = from, toDate = to });
    }

    [HttpGet("date-wise")]
    public IActionResult GetDateWiseAttendance(string date, int? classId, int? sectionId, int? batchId)
    {
        try
        {
            if (!DateTime.TryParse(date, out var selectedDate))
                return Ok(new List<object>());

            var records = svc.GetForDate(selectedDate, classId, sectionId, batchId)
                .Where(r => r.AttendanceId.HasValue)
                .ToList();

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

            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }

    [HttpGet("student-contact")]
    public IActionResult GetStudentContact(int studentId)
    {
        try
        {
            var student = studentSvc.GetById(studentId);
            if (student == null)
                return Ok(new { fatherPhone = "", motherPhone = "", studentPhone = "" });

            return Ok(new {
                fatherPhone = student.FatherPhone ?? "",
                motherPhone = student.MotherPhone ?? "",
                studentPhone = student.Phone ?? ""
            });
        }
        catch
        {
            return Ok(new { fatherPhone = "", motherPhone = "", studentPhone = "" });
        }
    }

    [HttpGet("added-by")]
    public IActionResult GetAddedByName(int studentId)
    {
        try
        {
            var history = svc.GetStudentAttendanceDetail(studentId);
            var lastRecord = history.OrderByDescending(x => x.AttendanceDate).FirstOrDefault();
            return Ok(new { name = "—" });
        }
        catch
        {
            return Ok(new { name = "—" });
        }
    }
}
