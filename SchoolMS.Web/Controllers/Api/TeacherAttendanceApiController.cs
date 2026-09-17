using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/teacher-attendance")]
[ApiRequireTeacherAttendanceAccess]
public class TeacherAttendanceApiController(
    TeacherAttendanceService attendanceSvc,
    FacultyService facultySvc,
    LookupService lookupSvc) : ControllerBase
{
    public record SaveAttendanceReq(int FacultyId, int? ClassId, int? BatchId, string? Subject,
        string? Topic, string? AttendanceDate, string? InTime, string? OutTime);

    [HttpGet]
    public IActionResult Index()
    {
        return Ok(new {
            teachers = facultySvc.GetAllActive(),
            classes = lookupSvc.GetClasses(),
            batches = lookupSvc.GetBatches(),
            attendance = attendanceSvc.GetAll(),
        });
    }

    [HttpPost("save")]
    public IActionResult SaveAttendance([FromBody] SaveAttendanceReq req)
    {
        try
        {
            var userId = User.UserId();
            if (!userId.HasValue) return Ok(new { success = false, message = "Session expired. Please login again." });

            DateTime date = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(req.AttendanceDate))
            {
                if (!DateTime.TryParse(req.AttendanceDate, out date))
                    return Ok(new { success = false, message = $"Invalid date: {req.AttendanceDate}. Use YYYY-MM-DD format." });
            }

            TimeSpan? inTimeSpan = null;
            TimeSpan? outTimeSpan = null;

            if (!string.IsNullOrWhiteSpace(req.InTime))
            {
                if (TimeSpan.TryParse(req.InTime.Trim(), out var in_ts))
                    inTimeSpan = in_ts;
                else
                    return Ok(new { success = false, message = $"Invalid In Time: {req.InTime}. Use HH:mm format (e.g., 09:30)." });
            }

            if (!string.IsNullOrWhiteSpace(req.OutTime))
            {
                if (TimeSpan.TryParse(req.OutTime.Trim(), out var out_ts))
                    outTimeSpan = out_ts;
                else
                    return Ok(new { success = false, message = $"Invalid Out Time: {req.OutTime}. Use HH:mm format (e.g., 17:00)." });
            }

            var attendanceId = attendanceSvc.Save(req.FacultyId, req.ClassId, req.BatchId, req.Subject, req.Topic,
                date, inTimeSpan, outTimeSpan, userId.Value);

            return Ok(new { success = true, attendanceId, message = "Attendance recorded successfully" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    [HttpPost("{attendanceId:int}/delete")]
    public IActionResult DeleteAttendance(int attendanceId)
    {
        try
        {
            var userId = User.UserId();
            if (!userId.HasValue) return Ok(new { success = false, message = "Unauthorized" });

            attendanceSvc.Delete(attendanceId, userId.Value);
            return Ok(new { success = true, message = "Attendance deleted successfully" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("summary")]
    public IActionResult Summary()
    {
        var attendance = attendanceSvc.GetAll();
        var (totalHours, totalDays) = attendanceSvc.GetSummary();

        return Ok(new { attendance, totalHours, totalDays });
    }
}
