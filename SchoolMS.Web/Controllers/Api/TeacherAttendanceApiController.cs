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
    LookupService lookupSvc,
    TeacherAttendanceNotifier notifier) : ControllerBase
{
    public record SaveAttendanceReq(int FacultyId, int? ClassId, int? BatchId, int? SectionId, string? Subject,
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
            sections = lookupSvc.GetSections().Where(s => s.IsActive).ToList(),
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

            var attendanceId = attendanceSvc.Save(req.FacultyId, classId, batchId, req.SectionId, req.Subject, req.Topic,
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

            var before = attendanceSvc.GetById(attendanceId);
            attendanceSvc.Delete(attendanceId, userId.Value);
            if (before != null) notifier.Deleted(before, userId.Value);   // the teacher is told: what, who, when
            return Ok(new { success = true, message = "Attendance deleted successfully" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Edit an entry (admin / anybody with the teacher attendance right; a Class Representative only his own recent ones). The teacher is told.</summary>
    [HttpPost("{attendanceId:int}/update")]
    public IActionResult UpdateAttendance(int attendanceId, [FromBody] SaveAttendanceReq req)
    {
        try
        {
            var userId = User.UserId();
            if (!userId.HasValue) return Ok(new { success = false, message = "Unauthorized" });
            var before = attendanceSvc.GetById(attendanceId);
            if (before == null) return Ok(new { success = false, message = "Entry not found (it may already be deleted)." });
            var cr = HttpContext.CurrentCr();
            if (cr != null && (before.CreatedBy != userId.Value || before.AttendanceDate.Date < CrScope.EarliestDate))
                return Ok(new { success = false, message = "You can edit only your own recent entries." });

            DateTime date = before.AttendanceDate;
            if (!string.IsNullOrWhiteSpace(req.AttendanceDate) && !DateTime.TryParse(req.AttendanceDate, out date))
                return Ok(new { success = false, message = $"Invalid date: {req.AttendanceDate}." });
            TimeSpan? inT = null, outT = null;
            if (!string.IsNullOrWhiteSpace(req.InTime)) { if (TimeSpan.TryParse(req.InTime.Trim(), out var a)) inT = a; else return Ok(new { success = false, message = "Invalid In Time." }); }
            if (!string.IsNullOrWhiteSpace(req.OutTime)) { if (TimeSpan.TryParse(req.OutTime.Trim(), out var b)) outT = b; else return Ok(new { success = false, message = "Invalid Out Time." }); }
            var invalid = TeacherAttendanceCrRules.Validate(req.Subject, req.Topic, inT, outT);
            if (invalid != null) return Ok(new { success = false, message = invalid });

            int? classId = req.ClassId, batchId = req.BatchId;
            if (cr != null)
            {
                var denied = TeacherAttendanceCrRules.Apply(cr, facultySvc, req.FacultyId, date, ref classId, ref batchId);
                if (denied != null) return Ok(new { success = false, message = denied });
            }

            attendanceSvc.Update(attendanceId, req.FacultyId, classId, batchId, req.SectionId, req.Subject, req.Topic, date, inT, outT, userId.Value);
            var after = attendanceSvc.GetById(attendanceId);
            if (after != null) notifier.Edited(before, after, userId.Value);   // the teacher is told: what, who, when
            return Ok(new { success = true, message = "Attendance updated successfully" });
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
