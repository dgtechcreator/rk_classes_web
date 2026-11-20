using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// Study follow-up (web): the summary (every call / oral test with date and time, filters, per-student totals), one student's
/// full history with the "add" form, and the Excel export. Right: study_followup.
/// </summary>
[RequireLogin]
public class StudyFollowupController(StudyFollowupService svc, LookupService lookup, StudentService students) : Controller
{
    int Uid => HttpContext.Session.GetUserId() ?? 0;
    bool IsAdmin => HttpContext.Session.GetInt32("RoleId") == 1;

    static StudyFollowupService.SummaryFilter Filter(string? q, string? kind, string? className, string? sectionName, string? batchName, int? followedBy,
        string? subject, string? result, string? status, DateTime? from, DateTime? to)
        => new() { Q = q, Kind = kind, ClassName = className, SectionName = sectionName, BatchName = batchName, FollowedBy = followedBy, Subject = subject, Result = result, Status = status, From = from?.Date, To = to?.Date };

    /// <summary>With no filter in the address the page shows the last 30 days; "All time" sends empty dates on purpose.</summary>
    public IActionResult Index(string? q, string? kind, string? className, string? sectionName, string? batchName, int? followedBy, string? subject,
        string? result, string? status, DateTime? from, DateTime? to, string? view)
    {
        if (!Request.Query.Any()) { from = DateTime.Today.AddDays(-29); to = DateTime.Today; }
        var summary = svc.Summary(Filter(q, kind, className, sectionName, batchName, followedBy, subject, result, status, from, to));
        ViewBag.Classes = lookup.GetClasses().Select(c => c.ClassName.Trim()).ToList();
        ViewBag.Sections = lookup.GetSections().Select(c => c.SectionName.Trim()).ToList();
        ViewBag.Batches = lookup.GetBatches().Select(c => c.BatchName.Trim()).ToList();
        ViewBag.Followers = svc.Details(null, null, null, null).Where(d => d.FollowedBy != null).GroupBy(d => d.FollowedBy!.Value).Select(g => (g.Key, g.First().FollowedByName)).OrderBy(x => x.FollowedByName).ToList();
        ViewBag.Subjects = svc.Subjects();
        ViewBag.Q = q; ViewBag.Kind = kind; ViewBag.ClassName = className; ViewBag.SectionName = sectionName; ViewBag.BatchName = batchName;
        ViewBag.FollowedBy = followedBy; ViewBag.Subject = subject; ViewBag.Result = result; ViewBag.Status = status;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.View = view == "students" ? "students" : "log";
        ViewData["Title"] = "Study Follow-up";
        return View(summary);
    }

    public IActionResult Student(int id, string? kind, DateTime? from, DateTime? to)
    {
        var card = svc.Card(id);
        if (card == null) return NotFound();
        var all = svc.History(id);
        ViewBag.Card = card;
        ViewBag.All = all;
        ViewBag.Kind = kind; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.IsAdmin = IsAdmin; ViewBag.Uid = Uid;
        ViewBag.Subjects = svc.Subjects();
        ViewData["Title"] = "Study Follow-up";
        return View(svc.History(id, kind, from, to));
    }

    [HttpPost]
    public IActionResult Add([FromBody] StudyAddReq req)
    {
        var (error, id) = svc.Add(StudyHelpers.ToEntity(req), Uid);
        return Json(new { success = error == null, error, id });
    }

    public record IdReq(int Id);

    [HttpPost]
    public IActionResult Delete([FromBody] IdReq req)
    {
        var error = svc.Delete(req.Id, Uid, IsAdmin);
        return Json(new { success = error == null, error });
    }

    [HttpGet]
    public IActionResult Students(string? q) => Json(StudyHelpers.SearchStudents(students, lookup, q));

    [HttpGet]
    public IActionResult Export(string? q, string? kind, string? className, string? sectionName, string? batchName, int? followedBy, string? subject,
        string? result, string? status, DateTime? from, DateTime? to)
        => File(StudyHelpers.Csv(svc.Summary(Filter(q, kind, className, sectionName, batchName, followedBy, subject, result, status, from, to))),
            "text/csv", $"StudyFollowups_{DateTime.Now:yyyyMMdd_HHmm}.csv");
}
