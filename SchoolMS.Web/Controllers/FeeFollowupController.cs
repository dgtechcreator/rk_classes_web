using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// Fee follow-up (web): the summary of every student with pending fees or a follow-up, one student's full history with the
/// "add follow-up" form, and the Excel export. Right: fees_due.
/// </summary>
[RequireLogin]
public class FeeFollowupController(FeeFollowupService svc, LookupService lookup) : Controller
{
    int Uid => HttpContext.Session.GetUserId() ?? 0;
    bool IsAdmin => HttpContext.Session.GetInt32("RoleId") == 1;

    static FeeFollowupService.SummaryFilter Filter(string? q, string? status, string? className, string? sectionName, string? batchName, int? followedBy, string? outcome, DateTime? from, DateTime? to)
        => new() { Q = q, Status = status, ClassName = className, SectionName = sectionName, BatchName = batchName, FollowedBy = followedBy, Outcome = outcome, From = from?.Date, To = to?.Date };

    public IActionResult Index(string? q, string? status, string? className, string? sectionName, string? batchName, int? followedBy, string? outcome, DateTime? from, DateTime? to)
    {
        var rows = svc.Summary(Filter(q, status, className, sectionName, batchName, followedBy, outcome, from, to));
        ViewBag.Classes = lookup.GetClasses().Select(c => c.ClassName.Trim()).ToList();
        ViewBag.Sections = lookup.GetSections().Select(c => c.SectionName.Trim()).ToList();
        ViewBag.Batches = lookup.GetBatches().Select(c => c.BatchName.Trim()).ToList();
        ViewBag.Followers = svc.Details(null, null, null, null).Where(d => d.FollowedBy != null).GroupBy(d => d.FollowedBy!.Value).Select(g => (g.Key, g.First().FollowedByName)).OrderBy(x => x.FollowedByName).ToList();
        ViewBag.Q = q; ViewBag.Status = status; ViewBag.ClassName = className; ViewBag.SectionName = sectionName; ViewBag.BatchName = batchName;
        ViewBag.FollowedBy = followedBy; ViewBag.Outcome = outcome; ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewData["Title"] = "Fee Follow-up";
        return View(rows);
    }

    public IActionResult Student(int id)
    {
        var fin = svc.Finance(id);
        var history = svc.History(id);
        if (fin == null && history.Count == 0) return NotFound();
        ViewBag.Fin = fin;
        ViewBag.IsAdmin = IsAdmin; ViewBag.Uid = Uid;
        ViewBag.StudentId = id;
        ViewBag.StudentName = fin?.FullName ?? history.FirstOrDefault()?.StudentName ?? "Student";
        ViewData["Title"] = "Fee Follow-up";
        return View(history);
    }

    public record AddReq(int StudentId, string Mode, string? SpokeTo, string Outcome, string? Reason, string? Notes, decimal? PromisedAmount, string? NextDate, string? NextDateBy, string? FollowupAt);

    [HttpPost]
    public IActionResult Add([FromBody] AddReq req)
    {
        var f = new FeeFollowup {
            StudentId = req.StudentId, Mode = req.Mode, SpokeTo = string.IsNullOrWhiteSpace(req.SpokeTo) ? null : req.SpokeTo, Outcome = req.Outcome, Reason = req.Reason, Notes = req.Notes,
            PromisedAmount = req.PromisedAmount, NextDate = DateTime.TryParse(req.NextDate, out var nd) ? nd.Date : null, NextDateBy = req.NextDateBy,
            FollowupAt = DateTime.TryParse(req.FollowupAt, out var at) ? at : DateTime.Now,
        };
        var (error, id) = svc.Add(f, Uid);
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
    public IActionResult Export(string? q, string? status, string? className, string? sectionName, string? batchName, int? followedBy, string? outcome, DateTime? from, DateTime? to)
    {
        var flt = Filter(q, status, className, sectionName, batchName, followedBy, outcome, from, to);
        var summary = svc.Summary(flt);
        var ids = summary.Select(s => s.StudentId).ToHashSet();
        var details = svc.Details(flt.From, flt.To, followedBy, outcome).Where(d => ids.Contains(d.StudentId)).ToList();
        return File(FollowupHelpers.Csv(summary, details), "text/csv", $"FeeFollowups_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }
}
