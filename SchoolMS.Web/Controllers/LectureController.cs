using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// Lecture schedule (web). Pages load their data as JSON from the *Json actions below — the same payloads the
/// mobile API returns (LectureHelpers), so web and app can never disagree.
/// lecture_schedule: View = all lectures, Edit = create / edit / cancel / delete. lecture_summary: View = counts.
/// "My Lectures" is for the logged-in teacher's own lectures and needs no module permission.
/// </summary>
public class LectureController(LectureService svc, LookupService lookup, MastersService masters,
    FacultyService faculty, TeacherAccountLinker linker) : Controller
{
    public record StatusReq(int Id, string Status, string? Note);
    public record IdReq(int Id);

    int Uid => HttpContext.Session.GetUserId() ?? 0;

    string OptionsJson() => JsonSerializer.Serialize(LectureHelpers.OptionsPayload(lookup, masters, faculty));

    static LectureFilter Filter(DateTime? from, DateTime? to, int? classId, int? sectionId, int? batchId,
        string? subject, int? facultyId, string? status)
    {
        var (f, t) = LectureHelpers.Range(from, to);
        return new LectureFilter { From = f, To = t, ClassId = classId, SectionId = sectionId, BatchId = batchId,
            Subject = subject, FacultyId = facultyId, Status = status };
    }

    // ── Schedule (admin / permitted staff) ───────────────────────────────────────────────────
    [RequirePermission("lecture_schedule")]
    public IActionResult Index()
    {
        ViewBag.OptionsJson = OptionsJson();
        ViewBag.CanEdit = HttpContext.Session.HasEditPerm("lecture_schedule");
        return View();
    }

    [RequirePermission("lecture_schedule")]
    [HttpGet]
    public IActionResult ListJson(DateTime? from, DateTime? to, int? classId, int? sectionId, int? batchId,
        string? subject, int? facultyId, string? status)
        => Json(LectureHelpers.ListPayload(svc, Filter(from, to, classId, sectionId, batchId, subject, facultyId, status)));

    [RequirePermission("lecture_schedule", requireEdit: true)]
    [HttpPost]
    public IActionResult Save([FromBody] LectureSaveReq req)
    {
        var (error, count) = svc.Save(req, Uid);
        if (error != null) return Json(new { success = false, message = error });
        var msg = req.LectureId > 0 ? "Lecture updated." : count == 1 ? "Lecture scheduled." : $"{count} lectures scheduled.";
        return Json(new { success = true, count, message = msg });
    }

    /// <summary>Teachers may mark only their own lectures Completed (or re-open them); everything else needs Edit permission.</summary>
    [RequireLogin]
    [HttpPost]
    public IActionResult SetStatus([FromBody] StatusReq req)
    {
        var l = svc.GetById(req.Id);
        if (l == null) return Json(new { success = false, message = "Lecture not found." });
        if (!HttpContext.Session.HasEditPerm("lecture_schedule"))
        {
            var mine = linker.FindFaculty(Uid);
            if (mine == null || mine.FacultyId != l.FacultyId || req.Status == LectureStatus.Cancelled)
                return Json(new { success = false, message = "Access denied." });
        }
        var error = svc.SetStatus(req.Id, req.Status, req.Note, Uid);
        return Json(new { success = error == null, message = error ?? "Updated." });
    }

    [RequirePermission("lecture_schedule", requireEdit: true)]
    [HttpPost]
    public IActionResult Delete([FromBody] IdReq req)
        => Json(new { success = svc.Delete(req.Id, Uid), message = "Lecture deleted." });

    [RequirePermission("lecture_schedule", requireEdit: true)]
    [HttpPost]
    public IActionResult DeleteSeries([FromBody] IdReq req)
    {
        var n = svc.DeleteSeriesFrom(req.Id, Uid);
        return Json(new { success = n > 0, message = $"{n} lectures deleted." });
    }

    // ── Summary ──────────────────────────────────────────────────────────────────────────────
    [RequirePermission("lecture_summary")]
    public IActionResult Summary()
    {
        ViewBag.OptionsJson = OptionsJson();
        return View();
    }

    [RequirePermission("lecture_summary")]
    [HttpGet]
    public IActionResult SummaryJson(DateTime? from, DateTime? to, string? groupBy, int? classId, int? sectionId,
        int? batchId, string? subject, int? facultyId)
        => Json(LectureHelpers.SummaryPayload(svc, Filter(from, to, classId, sectionId, batchId, subject, facultyId, null), groupBy));

    // ── My lectures (teacher) ────────────────────────────────────────────────────────────────
    [RequireLogin]
    public IActionResult Mine()
    {
        ViewBag.Linked = linker.FindFaculty(Uid) != null;
        return View();
    }

    [RequireLogin]
    [HttpGet]
    public IActionResult MineJson(DateTime? from, DateTime? to)
    {
        var f = linker.FindFaculty(Uid);
        if (f == null) return Json(new { linked = false });
        var today = DateTime.Today;
        var (rf, rt) = LectureHelpers.Range(from ?? today, to ?? today.AddDays(30));
        return Json(LectureHelpers.MinePayload(svc, f, rf, rt));
    }
}
