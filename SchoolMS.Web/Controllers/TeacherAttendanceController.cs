using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

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
        var teachers = facultySvc.GetAllActive();
        var classes = lookupSvc.GetClasses();
        var batches = lookupSvc.GetBatches();
        var attendance = attendanceSvc.GetAll();

        ViewBag.Teachers = teachers;
        ViewBag.Classes = classes;
        ViewBag.Batches = batches;
        ViewBag.Attendance = attendance;

        ViewData["Title"] = "Teachers Attendance";
        return View();
    }

    [HttpPost]
    public IActionResult SaveAttendance(int facultyId, int? classId, int? batchId, string? subject,
        string? topic, string? attendanceDate, string? inTime, string? outTime)
    {
        try
        {
            var userId = HttpContext.Session.GetUserId();
            if (!userId.HasValue) return Json(new { success = false, message = "Session expired. Please login again." });

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

            var attendanceId = attendanceSvc.Save(facultyId, classId, batchId, subject, topic,
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

        var attendance = attendanceSvc.GetAll();
        var (totalHours, totalDays) = attendanceSvc.GetSummary();

        ViewBag.Attendance = attendance;
        ViewBag.TotalHours = totalHours;
        ViewBag.TotalDays = totalDays;

        ViewData["Title"] = "My Attendance Summary";
        return View();
    }
}
