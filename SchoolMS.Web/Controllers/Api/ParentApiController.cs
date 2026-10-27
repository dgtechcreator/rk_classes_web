using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Utils;

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
    FeeStructureService feeStructureSvc,
    FeePositionService feePos,
    LookupService lookup,
    LectureService lectureSvc,
    ActivityNotifier activity,
    ClassNotesService notesSvc) : ControllerBase
{
    string Phone => User.Phone() ?? "";

    /// <summary>What happened to my children (absent, marks, fees, lectures), newest first. Opening the list clears the unread badge via POST notifications/read.</summary>
    [HttpGet("notifications")]
    public IActionResult Notifications(int? studentId, int take = 100)
    {
        try
        {
            var items = activity.ForParent(Phone, studentId, take);
            return Ok(new { items, unreadCount = activity.UnreadForParent(Phone) });
        }
        catch (Exception ex)
        {
            return Ok(new { items = Array.Empty<object>(), unreadCount = 0, error = ex.Message });
        }
    }

    [HttpGet("notifications/unread-count")]
    public IActionResult NotificationCount()
    {
        try { return Ok(new { unreadCount = activity.UnreadForParent(Phone) }); }
        catch { return Ok(new { unreadCount = 0 }); }
    }

    [HttpPost("notifications/read")]
    public IActionResult MarkNotificationsRead()
    {
        try { activity.MarkParentSeen(Phone); return Ok(new { success = true }); }
        catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
    }

    /// <summary>Notes and practice papers of my child's class (download through GET api/notes/{id}/file).</summary>
    [HttpGet("notes")]
    public IActionResult Notes(int studentId, string? kind)
    {
        var child = parentSvc.GetChildren(Phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        try
        {
            var list = notesSvc.Search(new ClassNoteFilter {
                ForStudentClassId = child.ClassId ?? 0, ForStudentSectionId = child.SectionId, ForStudentBatchId = child.BatchId, Kind = kind });
            return Ok(list.Select(n => new {
                noteId = n.NoteId, kind = n.Kind, title = n.Title, description = n.Description, subjectName = n.SubjectName,
                fileName = n.FileName, fileSize = n.FileSize, paperDate = n.PaperDate, createdByName = n.CreatedByName, createdAt = n.CreatedAt }));
        }
        catch (Exception) { return Ok(Array.Empty<object>()); }
    }

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

        var fee = feePos.Calculate(selected);

        return Ok(new {
            student = selected,
            attendance,
            attendanceDetail = attDetail,
            marks,
            feeHistory = fee.History,
            totalPaid = fee.Paid,
            actualFee = fee.BaseFee,          // base fee (older app builds)
            additionalCharges = fee.AdditionalCharges,
            discount = fee.Discount,
            netTotal = fee.NetTotal,          // fee + charges - discount
            balance = fee.Balance,
            dueDate = fee.InstallmentDate,    // next installment date (older app builds read this key)
            installmentDate = fee.InstallmentDate,
            feeStructures = fee.Structures,
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

    // Top 5 (overall + per subject) of the child's own class & medium — same data as the web parent portal.
    [HttpGet("toppers")]
    public IActionResult Toppers(int studentId)
    {
        var child = parentSvc.GetChildren(Phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        return Ok(ParentToppersHelper.Build(child, marksSvc, lookup));
    }

    // Lectures of the child's own class / medium / batch: the chosen day plus the counts for that day's month.
    [HttpGet("lectures")]
    public IActionResult Lectures(int studentId, DateTime? date)
    {
        var child = parentSvc.GetChildren(Phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        return Ok(LectureHelpers.ParentPayload(lectureSvc, child, date ?? DateTime.Today));
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
