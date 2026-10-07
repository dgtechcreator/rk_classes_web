using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Controllers;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/attendance")]
[ApiRequireStaff]
public class AttendanceApiController(AttendanceService svc, StudentService studentSvc, AttendanceBatchService batchSvc,
    FacultyService facultySvc, MastersService mastersSvc) : ControllerBase
{
    private static DateTime ClampDate(DateTime? date)
    {
        var d = (date ?? DateTime.Today).Date;
        return d > DateTime.Today ? DateTime.Today : d;
    }

    // Flat payload shared by the class-filter and batch endpoints (the mobile app parses these keys at the top level).
    private static Dictionary<string, object?> DayPayload(DateTime d, List<AttendanceRecord> records)
    {
        var first = records.FirstOrDefault(r => r.Subject != null || r.SirName != null);
        return new Dictionary<string, object?> {
            ["date"] = d,
            ["records"] = records,
            ["subject"] = first?.Subject,
            ["sirName"] = first?.SirName,
            ["startTime"] = first?.StartTime?.ToString(@"hh\:mm"),
            ["endTime"] = first?.EndTime?.ToString(@"hh\:mm"),
            ["totalPresent"] = records.Count(r => r.AttendanceStatus == "Present"),
            ["totalAbsent"] = records.Count(r => r.AttendanceStatus == "Absent"),
            ["totalLate"] = records.Count(r => r.AttendanceStatus == "Late"),
            // Only students that already have a saved row count as marked (unsaved ones default to Present).
            ["totalMarked"] = records.Count(r => r.AttendanceId.HasValue),
        };
    }

    [HttpGet]
    [ApiRequirePermission("attendance_entry")]
    public IActionResult Index(DateTime? date, int? classId, int? sectionId, int? batchId)
    {
        var d = ClampDate(date);
        var records = classId.HasValue || sectionId.HasValue || batchId.HasValue
            ? svc.GetForDate(d, classId, sectionId, batchId)
            : svc.GetForDate(d, null, null, null).Where(r => r.AttendanceId.HasValue).ToList();
        return Ok(DayPayload(d, records));
    }

    /// <summary>Attendance batch cards for one date: present/total per batch and whether it was marked yet.</summary>
    [HttpGet("batches")]
    [ApiRequirePermission("attendance_entry")]
    public IActionResult Batches(DateTime? date)
    {
        var d = ClampDate(date);
        var dayRecords = svc.GetForDate(d, null, null, null);
        var batches = batchSvc.BuildSummaries(batchSvc.GetAll(), dayRecords);
        return Ok(new {
            date = d,
            batches = batches.Select(b => new {
                b.BatchId, b.BatchName, b.Total, b.Present, b.Absent, b.Late, b.Marked, b.Pending, b.IsMarked,
            }),
        });
    }

    /// <summary>One attendance batch's students for a date — already-marked days come back filled in.</summary>
    [HttpGet("batch/{attBatchId:int}")]
    [ApiRequirePermission("attendance_entry")]
    public IActionResult BatchDetail(int attBatchId, DateTime? date)
    {
        var d = ClampDate(date);
        var batch = batchSvc.GetById(attBatchId);
        if (batch == null) return NotFound(new { error = "Attendance batch not found." });
        var records = batchSvc.RecordsFor(batch, svc.GetForDate(d, null, null, null));
        var payload = DayPayload(d, records);
        payload["batchId"] = batch.BatchId;
        payload["batchName"] = batch.BatchName;
        return Ok(payload);
    }

    /// <summary>Subject names and active faculty for the lecture-details fields.</summary>
    [HttpGet("options")]
    [ApiRequirePermission("attendance_entry")]
    public IActionResult Options()
    {
        var (teachers, _) = facultySvc.GetAll(null, "Active", null, 1, 1000);
        return Ok(new {
            subjects = mastersSvc.GetSubjects().Select(s => s.SubjectName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n),
            teachers = teachers.Select(t => new { fullName = t.FullName, displayName = t.DisplayName }),
        });
    }

    [HttpPost("save")]
    [ApiRequirePermission("attendance_entry")]
    public IActionResult Save([FromBody] AttSaveReq req)
    {
        int uid = User.UserId() ?? 1;
        var (ok, message) = AttendanceSaveHelper.Save(req, uid, svc, studentSvc, batchSvc);
        if (!ok) return BadRequest(new { success = false, error = message, message });
        return Ok(new { success = true, message });
    }

    [HttpGet("report")]
    [ApiRequirePermission("attendance_report")]
    public IActionResult Report(int? classId, int? sectionId, int? batchId, int? month, int? year)
    {
        var (report, phones) = AttendanceReportHelper.Build(classId, sectionId, batchId,
            month ?? DateTime.Today.Month, year ?? DateTime.Today.Year, svc, studentSvc);
        var studentPhones = phones.ToDictionary(kv => kv.Key, kv => new { father = kv.Value.father, mother = kv.Value.mother });
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
