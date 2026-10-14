using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Utils;

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
        var cr = HttpContext.CurrentCr();   // Class Representative: only his class / batch, only his own entries
        var classes = lookupSvc.GetClasses();
        var batches = lookupSvc.GetBatches();
        if (cr != null)
        {
            classes = classes.Where(c => c.ClassId == cr.ClassId).ToList();
            if (cr.EffectiveBatchId != null) batches = batches.Where(b => b.BatchId == cr.EffectiveBatchId).ToList();
        }
        return Ok(new {
            teachers = facultySvc.GetAllActive(),
            classes,
            batches,
            attendance = attendanceSvc.GetAll(createdBy: cr?.UserId),
            classRep = cr == null ? null : new { editDays = CrScope.EditDays, classId = cr.ClassId, batchId = cr.EffectiveBatchId },
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

            var invalid = TeacherAttendanceCrRules.Validate(req.Subject, req.Topic, inTimeSpan, outTimeSpan);
            if (invalid != null) return Ok(new { success = false, message = invalid });
            int? classId = req.ClassId, batchId = req.BatchId;
            var cr = HttpContext.CurrentCr();
            if (cr != null)
            {
                var denied = TeacherAttendanceCrRules.Apply(cr, facultySvc, req.FacultyId, date, ref classId, ref batchId);
                if (denied != null) return Ok(new { success = false, message = denied });
            }

            var attendanceId = attendanceSvc.Save(req.FacultyId, classId, batchId, req.Subject, req.Topic,
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

            if (HttpContext.CurrentCr() != null)
            {
                var rec = attendanceSvc.GetById(attendanceId);
                if (rec == null || rec.CreatedBy != userId.Value || rec.AttendanceDate.Date < CrScope.EarliestDate)
                    return Ok(new { success = false, message = "You can delete only your own recent entries." });
            }

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
        var onlyMine = HttpContext.CurrentCr() != null ? User.UserId() : null;
        var attendance = attendanceSvc.GetAll(createdBy: onlyMine);
        var (totalHours, totalDays) = attendanceSvc.GetSummary(createdBy: onlyMine);

        return Ok(new { attendance, totalHours, totalDays });
    }
}
