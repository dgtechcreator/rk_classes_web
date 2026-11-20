using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Study follow-up for the app: summary (entries + students), one student's history, add / delete, student search and the Excel export. Right: study_followup.</summary>
[ApiController]
[Route("api/study-followups")]
[ApiRequirePermission("study_followup")]
public class StudyFollowupsApiController(StudyFollowupService svc, LookupService lookup, StudentService students) : ControllerBase
{
    int Uid => User.UserId() ?? 0;

    static StudyFollowupService.SummaryFilter Filter(string? q, string? kind, string? className, string? sectionName, string? batchName, int? followedBy,
        string? subject, string? result, string? status, string? from, string? to)
        => new() {
            Q = q, Kind = kind, ClassName = className, SectionName = sectionName, BatchName = batchName, FollowedBy = followedBy, Subject = subject, Result = result, Status = status,
            From = DateTime.TryParse(from, out var f) ? f.Date : null, To = DateTime.TryParse(to, out var t) ? t.Date : null,
        };

    [HttpGet("options")]
    public IActionResult Options() => Ok(StudyHelpers.Options(svc.Subjects()));

    [HttpGet("summary")]
    public IActionResult Summary(string? q, string? kind, string? className, string? sectionName, string? batchName, int? followedBy, string? subject,
        string? result, string? status, string? from, string? to)
        => Ok(StudyHelpers.SummaryPayload(svc.Summary(Filter(q, kind, className, sectionName, batchName, followedBy, subject, result, status, from, to))));

    [HttpGet("student/{studentId:int}")]
    public IActionResult Student(int studentId, string? kind, string? from, string? to)
    {
        var card = svc.Card(studentId);
        if (card == null) return NotFound(new { success = false, message = "Student not found." });
        var all = svc.History(studentId);
        var shown = svc.History(studentId, kind, DateTime.TryParse(from, out var f) ? f.Date : null, DateTime.TryParse(to, out var t) ? t.Date : null);
        return Ok(StudyHelpers.StudentPayload(card, all, shown, Uid, User.RoleId() == 1));
    }

    [HttpGet("students")]
    public IActionResult Students(string? q) => Ok(StudyHelpers.SearchStudents(students, lookup, q));

    [HttpPost("add")]
    public IActionResult Add([FromBody] StudyAddReq req)
    {
        var (error, id) = svc.Add(StudyHelpers.ToEntity(req), Uid);
        return error != null ? BadRequest(new { success = false, error, message = error }) : Ok(new { success = true, id });
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        var error = svc.Delete(id, Uid, User.RoleId() == 1);
        return error != null ? BadRequest(new { success = false, error, message = error }) : Ok(new { success = true });
    }

    /// <summary>Excel (CSV). Opened by the app with ?token= (a link outside the app).</summary>
    [HttpGet("export")]
    public IActionResult Export(string? q, string? kind, string? className, string? sectionName, string? batchName, int? followedBy, string? subject,
        string? result, string? status, string? from, string? to)
        => File(StudyHelpers.Csv(svc.Summary(Filter(q, kind, className, sectionName, batchName, followedBy, subject, result, status, from, to))),
            "text/csv", $"StudyFollowups_{DateTime.Now:yyyyMMdd_HHmm}.csv");
}
