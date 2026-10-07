using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>
/// Lecture schedule for staff: admin / permitted staff manage and summarise everything, a teacher sees only
/// their own lectures ("mine"). Parents read their child's lectures from ParentApiController.
/// Permissions: lecture_schedule (View = see all, Edit = create / edit / cancel / delete), lecture_summary (View).
/// </summary>
[ApiController]
[Route("api/lectures")]
[ApiRequireStaff]
public class LecturesApiController(LectureService svc, LookupService lookup, MastersService masters,
    FacultyService faculty, TeacherAccountLinker linker) : ControllerBase
{
    public record StatusReq(string Status, string? Note);

    int Uid => User.UserId() ?? 0;

    static LectureFilter Filter(DateTime? from, DateTime? to, int? classId, int? sectionId, int? batchId,
        string? subject, int? facultyId, string? status)
    {
        var (f, t) = LectureHelpers.Range(from, to);
        return new LectureFilter { From = f, To = t, ClassId = classId, SectionId = sectionId, BatchId = batchId,
            Subject = subject, FacultyId = facultyId, Status = status };
    }

    /// <summary>Dropdown data for the schedule form and every filter bar.</summary>
    [HttpGet("options")]
    public IActionResult Options() => Ok(LectureHelpers.OptionsPayload(lookup, masters, faculty));

    [HttpGet]
    [ApiRequirePermission("lecture_schedule")]
    public IActionResult List(DateTime? from, DateTime? to, int? classId, int? sectionId, int? batchId,
        string? subject, int? facultyId, string? status)
        => Ok(LectureHelpers.ListPayload(svc, Filter(from, to, classId, sectionId, batchId, subject, facultyId, status)));

    [HttpGet("summary")]
    [ApiRequirePermission("lecture_summary")]
    public IActionResult Summary(DateTime? from, DateTime? to, string? groupBy, int? classId, int? sectionId, int? batchId,
        string? subject, int? facultyId)
        => Ok(LectureHelpers.SummaryPayload(svc, Filter(from, to, classId, sectionId, batchId, subject, facultyId, null), groupBy));

    [HttpPost("save")]
    [ApiRequirePermission("lecture_schedule", requireEdit: true)]
    public IActionResult Save([FromBody] LectureSaveReq req)
    {
        var (error, count) = svc.Save(req, Uid);
        if (error != null) return BadRequest(new { success = false, error, message = error });
        var msg = req.LectureId > 0 ? "Lecture updated." : count == 1 ? "Lecture scheduled." : $"{count} lectures scheduled.";
        return Ok(new { success = true, count, message = msg });
    }

    /// <summary>Mark Scheduled / Completed / Cancelled. Teachers may only mark THEIR OWN lectures Completed (or re-open them).</summary>
    [HttpPost("{id:int}/status")]
    public IActionResult Status(int id, [FromBody] StatusReq req)
    {
        var l = svc.GetById(id);
        if (l == null) return NotFound(new { error = "Lecture not found." });

        if (!User.HasEditPerm("lecture_schedule"))
        {
            var mine = linker.FindFaculty(Uid);
            if (mine == null || mine.FacultyId != l.FacultyId || req.Status == LectureStatus.Cancelled)
                return StatusCode(403, new { error = "Access denied." });
        }
        var error = svc.SetStatus(id, req.Status, req.Note, Uid);
        return error != null ? BadRequest(new { success = false, error }) : Ok(new { success = true });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("lecture_schedule", requireEdit: true)]
    public IActionResult Delete(int id)
        => svc.Delete(id, Uid) ? Ok(new { success = true }) : NotFound(new { error = "Lecture not found." });

    /// <summary>Deletes this lecture and all later not-yet-completed lectures of the same repeating schedule.</summary>
    [HttpPost("{id:int}/delete-series")]
    [ApiRequirePermission("lecture_schedule", requireEdit: true)]
    public IActionResult DeleteSeries(int id)
    {
        var n = svc.DeleteSeriesFrom(id, Uid);
        return n > 0 ? Ok(new { success = true, deleted = n }) : NotFound(new { error = "Lecture not found." });
    }

    /// <summary>The caller's OWN lectures (linked faculty profile) with this / last month counts. No id parameter to tamper with.</summary>
    [HttpGet("mine")]
    public IActionResult Mine(DateTime? from, DateTime? to)
    {
        var f = linker.FindFaculty(Uid);
        if (f == null)
            return Ok(new { linked = false, message = "Your login is not linked to a faculty profile yet. Please ask the admin." });
        var today = DateTime.Today;
        var (rf, rt) = LectureHelpers.Range(from ?? today, to ?? today.AddDays(30));
        return Ok(LectureHelpers.MinePayload(svc, f, rf, rt));
    }
}
