using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class MastersController(MastersService svc, FeeStructureService feeSvc, LookupService lookup) : Controller
{
    public IActionResult Index()
    {
        var currentYearId = lookup.GetCurrentYearId();
        ViewBag.Years           = svc.GetYears();
        ViewBag.Classes         = svc.GetClasses();
        ViewBag.Sections        = svc.GetSections();
        ViewBag.Batches         = svc.GetBatches();
        ViewBag.Subjects        = svc.GetSubjects();
        ViewBag.FeeTypes        = lookup.GetFeeTypes();
        ViewBag.FeeList         = feeSvc.GetAll(currentYearId, null, null);
        ViewBag.CurrentYearId   = currentYearId;
        ViewBag.CurrentYearName = svc.GetYears().FirstOrDefault(y => y.IsCurrent)?.YearName ?? "";
        return View();
    }

    // ── Academic Year ─────────────────────────────────────────
    [HttpPost]
    public IActionResult SaveYear(int yearId, string yearName, bool isCurrent, bool isActive = true)
    {
        try {
            svc.SaveYear(new AcademicYear { YearId=yearId, YearName=yearName, IsCurrent=isCurrent, IsActive=isActive });
            TempData["Success"] = $"Academic Year '{yearName}' saved.";
        } catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "years");
    }

    [HttpPost]
    public IActionResult DeleteYear(int id)
    {
        try { svc.DeleteYear(id); TempData["Success"] = "Academic Year deleted."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "years");
    }

    // ── Class ─────────────────────────────────────────────────
    [HttpPost]
    public IActionResult SaveClass(int classId, string className, int orderNo, bool isActive = true)
    {
        try {
            svc.SaveClass(new Class { ClassId=classId, ClassName=className, OrderNo=orderNo, IsActive=isActive });
            TempData["Success"] = $"Class '{className}' saved.";
        } catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "classes");
    }

    [HttpPost]
    public IActionResult DeleteClass(int id)
    {
        try { svc.DeleteClass(id); TempData["Success"] = "Class deleted."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "classes");
    }

    // ── Section ───────────────────────────────────────────────
    [HttpPost]
    public IActionResult SaveSection(int sectionId, string sectionName, bool isActive = true)
    {
        try {
            svc.SaveSection(new Section { SectionId=sectionId, SectionName=sectionName, IsActive=isActive });
            TempData["Success"] = $"Section '{sectionName}' saved.";
        } catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "sections");
    }

    [HttpPost]
    public IActionResult DeleteSection(int id)
    {
        try { svc.DeleteSection(id); TempData["Success"] = "Section deleted."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "sections");
    }

    // ── Batch ─────────────────────────────────────────────────
    [HttpPost]
    public IActionResult SaveBatch(int batchId, string batchName, bool isActive = true)
    {
        try {
            svc.SaveBatch(new Batch { BatchId=batchId, BatchName=batchName, IsActive=isActive });
            TempData["Success"] = $"Batch '{batchName}' saved.";
        } catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "batches");
    }

    [HttpPost]
    public IActionResult DeleteBatch(int id)
    {
        try { svc.DeleteBatch(id); TempData["Success"] = "Batch deleted."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "batches");
    }

    // ── Subject ───────────────────────────────────────────────
    [HttpPost]
    public IActionResult SaveSubject(int subjectId, string subjectName, string? subjectCode, int classId, int maxMarks = 100, int passMarks = 35, bool isActive = true)
    {
        try {
            svc.SaveSubject(new Subject { SubjectId=subjectId, SubjectName=subjectName, SubjectCode=subjectCode, ClassId=classId, MaxMarks=maxMarks, PassMarks=passMarks, IsActive=isActive });
            TempData["Success"] = $"Subject '{subjectName}' saved.";
        } catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "subjects");
    }

    [HttpPost]
    public IActionResult DeleteSubject(int id)
    {
        try { svc.DeleteSubject(id); TempData["Success"] = "Subject deleted."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "subjects");
    }
}
