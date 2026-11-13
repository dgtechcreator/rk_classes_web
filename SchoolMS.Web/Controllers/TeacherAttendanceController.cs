using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers;

[RequireTeacherAttendanceAccess]
public class TeacherAttendanceController(
    TeacherAttendanceService attendanceSvc,
    FacultyService facultySvc,
    LookupService lookupSvc) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var cr = HttpContext.CurrentCr();   // Class Representative: only his class / batch, only his own entries
        var teachers = facultySvc.GetAllActive();
        var classes = lookupSvc.GetClasses();
        var batches = lookupSvc.GetBatches();
        var attendance = attendanceSvc.GetAll(createdBy: cr?.UserId);
        if (cr != null)
        {
            classes = classes.Where(c => c.ClassId == cr.ClassId).ToList();
            if (cr.EffectiveBatchId != null) batches = batches.Where(b => b.BatchId == cr.EffectiveBatchId).ToList();
        }
        ViewBag.Cr = cr;
        ViewBag.CrEarliest = CrScope.EarliestDate;

        ViewBag.Teachers = teachers;
        ViewBag.Classes = classes;
        ViewBag.Batches = batches;
        ViewBag.Sections = lookupSvc.GetSections().Where(s => s.IsActive).ToList();
        ViewBag.Attendance = attendance;

        ViewData["Title"] = "Teachers Attendance";
        return View();
    }

    [HttpPost]
    public IActionResult SaveAttendance(int facultyId, int? classId, int? batchId, int? sectionId, string? subject,
        string? topic, string? attendanceDate, string? inTime, string? outTime)
    {
        try
        {
            var userId = HttpContext.Session.GetUserId();
            if (!userId.HasValue) return Json(new { success = false, message = "Session expired. Please login again." });
            var cr = HttpContext.CurrentCr();

            // Parse date
            DateTime date = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(attendanceDate))
            {
                if (!DateTime.TryParse(attendanceDate, out date))
                {
                    return Json(new { success = false, message = $"Invalid date: {attendanceDate}. Use YYYY-MM-DD format." });
                }
            }

            TimeSpan? inTimeSpan = null;
            TimeSpan? outTimeSpan = null;

            // Parse in time
            if (!string.IsNullOrWhiteSpace(inTime))
            {
                inTime = inTime.Trim();
                if (TimeSpan.TryParse(inTime, out var in_ts))
                {
                    inTimeSpan = in_ts;
                }
                else
                {
                    return Json(new { success = false, message = $"Invalid In Time: {inTime}. Use HH:mm format (e.g., 09:30)." });
                }
            }

            // Parse out time
            if (!string.IsNullOrWhiteSpace(outTime))
            {
                outTime = outTime.Trim();
                if (TimeSpan.TryParse(outTime, out var out_ts))
                {
                    outTimeSpan = out_ts;
                }
                else
                {
                    return Json(new { success = false, message = $"Invalid Out Time: {outTime}. Use HH:mm format (e.g., 17:00)." });
                }
            }

            var invalid = TeacherAttendanceCrRules.Validate(subject, topic, inTimeSpan, outTimeSpan);
            if (invalid != null) return Json(new { success = false, message = invalid });
            if (cr != null)
            {
                var denied = TeacherAttendanceCrRules.Apply(cr, facultySvc, facultyId, date, ref classId, ref batchId);
                if (denied != null) return Json(new { success = false, message = denied });
            }

            var attendanceId = attendanceSvc.Save(facultyId, classId, batchId, sectionId, subject, topic,
                date, inTimeSpan, outTimeSpan, userId.Value);

            return Json(new { success = true, attendanceId, message = "Attendance recorded successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    [HttpPost]
    public IActionResult DeleteAttendance(int attendanceId)
    {
        try
        {
            var userId = HttpContext.Session.GetUserId();
            if (!userId.HasValue) return Json(new { success = false, message = "Unauthorized" });

            // A Class Representative may remove only the entries he made himself, and only while still editable.
            if (HttpContext.CurrentCr() != null)
            {
                var rec = attendanceSvc.GetById(attendanceId);
                if (rec == null || rec.CreatedBy != userId.Value || rec.AttendanceDate.Date < CrScope.EarliestDate)
                    return Json(new { success = false, message = "You can delete only your own recent entries." });
            }

            attendanceSvc.Delete(attendanceId, userId.Value);
            return Json(new { success = true, message = "Attendance deleted successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Summary()
    {
        // Note: In a real system, you would get the current faculty/teacher ID from the authenticated user
        // For now, show all records. To filter by teacher, you would do:
        // var currentFacultyId = GetCurrentFacultyIdFromAuth();
        // var attendance = attendanceSvc.GetAll(currentFacultyId);

        var onlyMine = HttpContext.CurrentCr() != null ? HttpContext.Session.GetUserId() : null;
        var attendance = attendanceSvc.GetAll(createdBy: onlyMine);
        var (totalHours, totalDays) = attendanceSvc.GetSummary(createdBy: onlyMine);

        ViewBag.Attendance = attendance;
        ViewBag.TotalHours = totalHours;
        ViewBag.TotalDays = totalDays;

        ViewData["Title"] = "My Attendance Summary";
        return View();
    }

    /// <summary>Every lecture as a file Excel opens (CSV, UTF-8 with BOM), then the class + medium wise total of each teacher.</summary>
    [HttpGet]
    public IActionResult ExportSummary(int? facultyId = null)
    {
        var onlyMine = HttpContext.CurrentCr() != null ? HttpContext.Session.GetUserId() : null;
        var list = attendanceSvc.GetAll(facultyId, onlyMine);
        string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        string T(TimeSpan? t) => t.HasValue ? DateTime.Today.Add(t.Value).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture) : "";
        string N(decimal v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Teacher,Date,Class,Stream / Medium,Batch,Subject,Topic,In Time,Out Time,Hours");
        foreach (var a in list.OrderBy(a => (a.FacultyName ?? "").Trim()).ThenBy(a => a.AttendanceDate))
            sb.AppendLine(string.Join(",", Q(a.FacultyName?.Trim()), a.AttendanceDate.ToString("dd-MM-yyyy"), Q(a.ClassName), Q(a.SectionName), Q(a.BatchName),
                Q(a.SubjectName), Q(a.Topic), T(a.InTime), T(a.OutTime), a.TotalHours.HasValue ? N(a.TotalHours.Value) : ""));
        sb.AppendLine();
        sb.AppendLine("Class and medium wise total");
        sb.AppendLine("Teacher,Class,Stream / Medium,Lectures,Days,Hours");
        foreach (var g in list.GroupBy(a => new { Name = (a.FacultyName ?? "").Trim(), Class = a.ClassName ?? "", Medium = a.SectionName ?? "" })
                              .OrderBy(g => g.Key.Name).ThenBy(g => g.Key.Class).ThenBy(g => g.Key.Medium))
            sb.AppendLine(string.Join(",", Q(g.Key.Name), Q(g.Key.Class), Q(g.Key.Medium), g.Count(), g.Select(x => x.AttendanceDate.Date).Distinct().Count(), N(g.Sum(x => x.TotalHours ?? 0))));
        sb.AppendLine($"Total,,,{list.Count},,{N(list.Sum(x => x.TotalHours ?? 0))}");
        var bytes = new System.Text.UTF8Encoding(true).GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"TeacherAttendance_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }
}
