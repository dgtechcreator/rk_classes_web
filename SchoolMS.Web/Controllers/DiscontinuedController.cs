using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// <summary>Discontinued Students: students who left the classes or were removed - with the reason, who recorded it and what was paid / pending.</summary>
[RequireLogin]
public class DiscontinuedController(DiscontinuedService svc, StudentService students, LookupService lookup) : Controller
{
    public IActionResult Index(string? q = null, string? kind = null)
    {
        var list = svc.GetAll();
        if (!string.IsNullOrWhiteSpace(kind) && DiscontinueKind.IsValid(kind)) list = list.Where(x => x.Kind == kind).ToList();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim().ToLowerInvariant();
            list = list.Where(x => $"{x.StudentName} {x.AdmissionNo} {x.Reason} {x.ClassName} {x.RecordedByName}".ToLowerInvariant().Contains(t)).ToList();
        }
        ViewBag.Q = q; ViewBag.Kind = kind;
        return View(list);
    }

    public IActionResult New()
    {
        ViewBag.Classes = lookup.GetClasses();
        ViewBag.Sections = lookup.GetSections();
        ViewBag.Batches = lookup.GetBatches();
        return View();
    }

    /// <summary>Active students of a class (optionally one medium / batch) for the student dropdown.</summary>
    [HttpGet]
    public IActionResult Students(int? classId, int? sectionId, int? batchId)
    {
        if (classId == null) return Json(Array.Empty<object>());
        var (list, _) = students.GetAll(1, 5000, null, classId, sectionId, batchId, null, "Active");
        return Json(list.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).Select(s => new {
            studentId = s.StudentId, name = s.DisplayName, admissionNo = s.AdmissionNo,
            medium = (s.SectionName ?? "").Trim(), batch = (s.BatchName ?? "").Trim(),
        }));
    }

    /// <summary>Active students matching a name / admission no / phone, for the picker.</summary>
    [HttpGet]
    public IActionResult Search(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Json(Array.Empty<object>());
        var (list, _) = students.GetAll(1, 25, q, null, null, null, null, "Active");
        return Json(list.Select(s => new {
            studentId = s.StudentId, name = s.DisplayName, admissionNo = s.AdmissionNo,
            cls = $"{(s.ClassName ?? "").Trim()} {(s.SectionName ?? "").Trim()} {(s.BatchName ?? "").Trim()}".Trim(),
        }));
    }

    /// <summary>The full picture of one student (HTML fragment) shown on the New page once a student is picked.</summary>
    [HttpGet]
    public IActionResult Overview(int studentId)
    {
        var o = svc.Overview(studentId);
        if (o == null) return NotFound();
        return PartialView("_Overview", o);
    }

    [HttpPost]
    public IActionResult Revert(int id, string? note)
    {
        var name = HttpContext.Session.GetString("DisplayName") ?? HttpContext.Session.GetString("FullName") ?? "";
        var (ok, error) = svc.Revert(id, note, HttpContext.Session.GetUserId() ?? 0, name);
        return Json(new { success = ok, message = ok ? "The student is active again and counted everywhere." : error });
    }

    [HttpPost]
    public IActionResult Save(int studentId, string? kind, string? reason, DateTime? leftOn)
    {
        var name = HttpContext.Session.GetString("DisplayName") ?? HttpContext.Session.GetString("FullName") ?? "";
        var (ok, error) = svc.Discontinue(studentId, kind, reason, leftOn ?? DateTime.Today, HttpContext.Session.GetUserId() ?? 0, name);
        if (!ok) return Json(new { success = false, message = error });
        return Json(new { success = true, message = "Saved. The student is now inactive and no longer counted anywhere." });
    }
}
