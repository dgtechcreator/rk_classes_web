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
    ClassNotesService notesSvc,
    StudentService studentSvc,
    ProfilePhotoStore photos,
    PortalAccessService portal,
    BirthdayService birthdays) : ControllerBase
{
    string Phone => User.Phone() ?? "";

    /// <summary>Sections the admin switched off for Student / Parent logins (Roles &amp; Access on the website). The app hides those tabs.</summary>
    /// <summary>Birthday cards for today (the wish text is the "Birthday" template from Masters). Empty on every other day.</summary>
    [HttpGet("birthdays")]
    public IActionResult Birthdays() => Ok(birthdays.ForParent(Phone).Select(b => new { studentId = b.StudentId, name = b.Name, title = b.Title, message = b.Message }));

    [HttpGet("portal-access")]
    public IActionResult PortalAccess() => Ok(new { hidden = portal.Hidden().OrderBy(x => x).ToList() });

    /// <summary>A parent sets / replaces their own child's profile photo (multipart field "photo"). Same file + DB column the staff side uses.</summary>
    [HttpPost("students/{id:int}/photo")]
    public IActionResult UploadChildPhoto(int id, IFormFile? photo)
    {
        if (!parentSvc.GetChildren(Phone).Any(c => c.StudentId == id)) return NotFound(new { error = "Student not found." });
        var s = studentSvc.GetById(id);
        if (s == null) return NotFound(new { error = "Student not found." });
        var (path, err) = photos.Save(photo, "students");
        if (err != null) return BadRequest(new { error = err });
        studentSvc.SetPhoto(id, path);
        photos.Delete(s.ProfilePicPath);
        return Ok(new { profilePicPath = path });
    }

    [HttpDelete("students/{id:int}/photo")]
    public IActionResult DeleteChildPhoto(int id)
    {
        if (!parentSvc.GetChildren(Phone).Any(c => c.StudentId == id)) return NotFound(new { error = "Student not found." });
        var s = studentSvc.GetById(id);
        if (s == null) return NotFound(new { error = "Student not found." });
        studentSvc.SetPhoto(id, null);
        photos.Delete(s.ProfilePicPath);
        return Ok(new { profilePicPath = (string?)null });
    }

    /// <summary>What happened to my children (absent, marks, fees, lectures), newest first. Opening the list clears the unread badge via POST notifications/read.</summary>
    [HttpGet("notifications")]
    [PortalSection("notifications")]
    public IActionResult Notifications(int? studentId, int take = 100)
    {
        try
        {
            var hidden = portal.Hidden();   // notifications of a switched-off section (e.g. lectures) are not shown
            var items = activity.ForParent(Phone, studentId, take, hidden);
            return Ok(new { items, unreadCount = activity.UnreadForParent(Phone, hidden) });
        }
        catch (Exception ex)
        {
            return Ok(new { items = Array.Empty<object>(), unreadCount = 0, error = ex.Message });
        }
    }

    [HttpGet("notifications/unread-count")]
    [PortalSection("notifications")]
    public IActionResult NotificationCount()
    {
        try { return Ok(new { unreadCount = activity.UnreadForParent(Phone, portal.Hidden()) }); }
        catch { return Ok(new { unreadCount = 0 }); }
    }

    [HttpPost("notifications/read")]
    [PortalSection("notifications")]
    public IActionResult MarkNotificationsRead()
    {
        try { activity.MarkParentSeen(Phone); return Ok(new { success = true }); }
        catch (Exception ex) { return Ok(new { success = false, message = ex.Message }); }
    }

    /// <summary>Notes and practice papers of my child's class (download through GET api/notes/{id}/file).</summary>
    [HttpGet("notes")]
    [PortalSection("notes")]
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
        // Sections switched off for Student / Parent logins are left empty here, not just hidden in the app.
        var hidden = portal.Hidden();
        var attendance = hidden.Contains("attendance") ? null : attSvc.GetStudentAttendanceSummary(sid);
        var attDetail  = hidden.Contains("attendance") ? new List<StudentAttendanceDetail>() : attSvc.GetStudentAttendanceDetail(sid);
        var marks      = hidden.Contains("marks") ? new List<TestMark>() : marksSvc.GetStudentAllMarks(sid);

        var fee = feePos.Calculate(selected);
        bool noFees = hidden.Contains("fees");

        return Ok(new {
            student = selected,
            attendance,
            attendanceDetail = attDetail,
            marks,
            feeHistory = noFees ? new() : fee.History,
            totalPaid = noFees ? 0 : fee.Paid,
            actualFee = noFees ? 0 : fee.BaseFee,          // base fee (older app builds)
            additionalCharges = noFees ? 0 : fee.AdditionalCharges,
            discount = noFees ? 0 : fee.Discount,
            netTotal = noFees ? 0 : fee.NetTotal,          // fee + charges - discount
            balance = noFees ? 0 : fee.Balance,
            previousFees = noFees ? 0 : fee.PreviousYearsFee,      // fees of earlier years (already inside the totals)
            previousDue = noFees ? 0 : fee.PreviousYearsDue,       // part of the balance still owed from earlier years
            dueDate = noFees ? null : fee.InstallmentDate,    // next installment date (older app builds read this key)
            installmentDate = noFees ? null : fee.InstallmentDate,
            feeStructures = noFees ? new() : fee.Structures,
        });
    }

    [HttpGet("top5")]
    [PortalSection("marks")]
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
    [PortalSection("marks")]
    public IActionResult Toppers(int studentId)
    {
        var child = parentSvc.GetChildren(Phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        return Ok(ParentToppersHelper.Build(child, marksSvc, lookup));
    }

    // Lectures of the child's own class / medium / batch: the chosen day plus the counts for that day's month.
    [HttpGet("lectures")]
    [PortalSection("lectures")]
    public IActionResult Lectures(int studentId, DateTime? date)
    {
        var child = parentSvc.GetChildren(Phone).FirstOrDefault(c => c.StudentId == studentId);
        if (child == null) return NotFound(new { error = "Student not found for this parent." });
        return Ok(LectureHelpers.ParentPayload(lectureSvc, child, date ?? DateTime.Today));
    }

    public record ContactReq(string Name, string Email, string Subject, string Message);

    // Mirrors ParentController.SendMessage — currently a stub server-side (no email integration yet).
    [HttpPost("contact")]
    [PortalSection("contact")]
    public IActionResult Contact(ContactReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "All fields are required." });

        return Ok(new { success = true, message = "Your message has been sent! We'll get back to you soon." });
    }
}
