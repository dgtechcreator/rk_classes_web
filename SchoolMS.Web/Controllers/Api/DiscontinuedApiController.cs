using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Discontinued Students for the mobile app - same rules as the website's /Discontinued pages.</summary>
[ApiController]
[Route("api/discontinued")]
[ApiRequirePermission("discontinued_students")]
public class DiscontinuedApiController(DiscontinuedService svc, StudentService students, StaffNameService names) : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(svc.GetAll().Select(d => new {
        d.Id, d.StudentId, studentName = d.StudentName, d.AdmissionNo, d.ClassName, d.SectionName, d.BatchName, d.FatherPhone,
        d.Kind, d.Reason, leftOn = d.LeftOn, d.TotalFees, d.Discount, d.Paid, d.Pending, d.AttendancePct, d.MarksPct,
        d.RecordedByName, d.RecordedAt, returned = d.Returned, canRevert = d.CanRevert,
        revertedAt = d.RevertedAt, revertedByName = d.RevertedByName, revertNote = d.RevertNote,
    }));

    /// <summary>Active students of a class (optionally one medium / batch) for the student picker.</summary>
    [HttpGet("students")]
    public IActionResult Students(int? classId, int? sectionId, int? batchId)
    {
        if (classId == null) return Ok(Array.Empty<object>());
        var (list, _) = students.GetAll(1, 5000, null, classId, sectionId, batchId, null, "Active");
        return Ok(list.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).Select(s => new {
            studentId = s.StudentId, name = s.DisplayName, admissionNo = s.AdmissionNo,
            medium = (s.SectionName ?? "").Trim(), batch = (s.BatchName ?? "").Trim(),
        }));
    }

    /// <summary>The full picture of one student before they are taken off the roll.</summary>
    [HttpGet("overview/{studentId:int}")]
    public IActionResult Overview(int studentId)
    {
        var o = svc.Overview(studentId);
        if (o == null) return NotFound(new { error = "Student not found." });
        var s = o.Student; var f = o.Fee; var a = o.Attendance;
        return Ok(new {
            studentId = s.StudentId, name = s.DisplayName, admissionNo = s.AdmissionNo, status = s.Status,
            className = (s.ClassName ?? "").Trim(), sectionName = (s.SectionName ?? "").Trim(), batchName = (s.BatchName ?? "").Trim(),
            admissionDate = s.AdmissionDate, profilePicPath = s.ProfilePicPath, address = s.Address,
            fatherName = s.FatherName, fatherPhone = s.FatherPhone, motherName = s.MotherName, motherPhone = s.MotherPhone, phone = s.Phone,
            attendance = a == null ? null : new { a.PresentDays, a.AbsentDays, a.LateDays, a.TotalDays, pct = Math.Min(100m, a.AttendancePct) },
            marksPct = o.MarksPct, testsCount = o.TestsCount,
            fee = new { baseFee = f.BaseFee, additionalCharges = f.AdditionalCharges, discount = f.Discount, total = f.NetTotal, paid = f.Paid, balance = f.Balance, previousDue = f.PreviousYearsDue },
            payments = f.History.OrderByDescending(p => p.PaymentDate).Take(6).Select(p => new { date = p.PaymentDate, mode = p.PaymentMode, amount = p.NetAmount }),
        });
    }

    public record RevertReq(string? Note);

    /// <summary>Take a discontinued student back (Active again; the entry stays as history).</summary>
    [HttpPost("{id:int}/revert")]
    [ApiRequirePermission("discontinued_students", requireEdit: true)]
    public IActionResult Revert(int id, [FromBody] RevertReq? req)
    {
        var uid = User.UserId() ?? 0;
        string name;
        try { name = names.DisplayName(uid); } catch { name = ""; }
        var (ok, error) = svc.Revert(id, req?.Note, uid, name);
        return ok ? Ok(new { success = true }) : BadRequest(new { error });
    }

    public record SaveReq(int StudentId, string? Kind, string? Reason, DateTime? LeftOn);

    [HttpPost("save")]
    [ApiRequirePermission("discontinued_students", requireEdit: true)]
    public IActionResult Save([FromBody] SaveReq req)
    {
        var uid = User.UserId() ?? 0;
        string name;
        try { name = names.DisplayName(uid); } catch { name = ""; }
        var (ok, error) = svc.Discontinue(req.StudentId, req.Kind, req.Reason, req.LeftOn ?? DateTime.Today, uid, name);
        return ok ? Ok(new { success = true }) : BadRequest(new { error });
    }
}
