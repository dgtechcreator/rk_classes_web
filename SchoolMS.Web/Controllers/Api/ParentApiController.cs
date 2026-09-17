using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

// Mirrors ParentController's Dashboard/GetTop5ForSubject business logic exactly, JSON instead of a view.
[ApiController]
[Route("api/parent")]
[ApiRequireParent]
public class ParentApiController(
    ParentService parentSvc,
    AttendanceService attSvc,
    MarksService marksSvc,
    FeesService feesSvc,
    FeeStructureService feeStructureSvc) : ControllerBase
{
    string Phone => User.Phone() ?? "";

    [HttpGet("children")]
    public IActionResult Children() => Ok(parentSvc.GetChildren(Phone));

    [HttpGet("dashboard/{studentId:int}")]
    public IActionResult Dashboard(int studentId)
    {
        var children = parentSvc.GetChildren(Phone);
        var selected = children.FirstOrDefault(c => c.StudentId == studentId);
        if (selected == null) return NotFound(new { error = "Student not found for this parent." });

        var sid = selected.StudentId;
        var attendance = attSvc.GetStudentAttendanceSummary(sid);
        var attDetail  = attSvc.GetStudentAttendanceDetail(sid);
        var marks      = marksSvc.GetStudentAllMarks(sid);
        var feeHistory = feesSvc.GetStudentHistory(sid);
        var totalPaid  = feeHistory.Sum(x => x.NetAmount);

        var structures = feeStructureSvc.GetAllForStudent(sid);
        if (!structures.Any() && selected.AcademicYearId.HasValue && selected.ClassId.HasValue)
        {
            structures = feeStructureSvc.GetAll(selected.AcademicYearId, selected.ClassId, selected.SectionId);
            if (!structures.Any() && selected.SectionId.HasValue)
                structures = feeStructureSvc.GetAll(selected.AcademicYearId, selected.ClassId, null);
            if (!structures.Any())
                structures = feeStructureSvc.GetAll(null, selected.ClassId, selected.SectionId);
            if (!structures.Any())
                structures = feeStructureSvc.GetAll(null, selected.ClassId, null);
        }
        var actualFee = structures.Sum(x => x.Amount);

        List<object>? classFeesSummary = null;
        if (selected.ClassId.HasValue)
            classFeesSummary = feesSvc.GetClassFeesSummary(selected.ClassId.Value, selected.SectionId).Cast<object>().ToList();

        return Ok(new {
            student = selected,
            attendance,
            attendanceDetail = attDetail,
            marks,
            feeHistory,
            totalPaid,
            actualFee,
            balance = actualFee - totalPaid,
            classFeesSummary,
        });
    }

    [HttpGet("top5")]
    public IActionResult Top5(int studentId, string subjectName)
    {
        var children = parentSvc.GetChildren(Phone);
        var student = children.FirstOrDefault(c => c.StudentId == studentId);
        if (student?.ClassId == null) return Ok(new { top5 = new List<object>() });

        var top5 = marksSvc.GetTop5StudentsInSubject(subjectName, student.ClassId.Value, student.SectionId);
        return Ok(new { top5 });
    }

    public record ContactReq(string Name, string Email, string Subject, string Message);

    // Mirrors ParentController.SendMessage — currently a stub server-side (no email integration yet).
    [HttpPost("contact")]
    public IActionResult Contact(ContactReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "All fields are required." });

        return Ok(new { success = true, message = "Your message has been sent! We'll get back to you soon." });
    }
}
