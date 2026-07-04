using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class MastersController(MastersService svc, FeeStructureService feeSvc, LookupService lookup, StudentService studentSvc, AttendanceBatchService batchSvc) : Controller
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
        ViewBag.FeeList           = feeSvc.GetAll(null, null, null);
        ViewBag.CurrentYearId     = currentYearId;
        ViewBag.CurrentYearName   = svc.GetYears().FirstOrDefault(y => y.IsCurrent)?.YearName ?? "";
        ViewBag.ExpenseCategories = svc.GetExpenseCategories();
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
    public IActionResult SaveSubject(int subjectId, string subjectName, string? subjectCode, int classId = 0, int maxMarks = 100, int passMarks = 35, bool isActive = true)
    {
        try {
            svc.SaveSubject(new Subject { SubjectId=subjectId, SubjectName=subjectName, SubjectCode=subjectCode, ClassId=classId==0?(int?)null:classId, MaxMarks=maxMarks, PassMarks=passMarks, IsActive=isActive });
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

    // ── Expense Categories ────────────────────────────────────
    [HttpPost]
    public IActionResult SaveExpenseCat(int categoryId, string categoryName, bool isActive = true)
    {
        try {
            svc.SaveExpenseCat(new ExpenseCat { CategoryId=categoryId, CategoryName=categoryName, IsActive=isActive });
            TempData["Success"] = $"Expense category '{categoryName}' saved.";
        } catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "expcats");
    }

    [HttpPost]
    public IActionResult DeleteExpenseCat(int id)
    {
        try { svc.DeleteExpenseCat(id); TempData["Success"] = "Expense category deleted."; }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction("Index", null, "expcats");
    }

    // ── Attendance Batches ─────────────────────────────────────
    [HttpGet]
    public IActionResult AttendanceBatches()
    {
        ViewBag.Classes = svc.GetClasses();
        ViewBag.Sections = svc.GetSections();
        ViewBag.Batches = svc.GetBatches();
        return View();
    }

    [HttpGet]
    public IActionResult GetStudentsForBatch(int classId, int? sectionId, int? batchId)
    {
        try
        {
            var (students, _) = studentSvc.GetAll(1, 10000, null, classId > 0 ? classId : null,
                sectionId, batchId, null, "Active");
            return Json(students.Select(s => new {
                studentId = s.StudentId,
                fullName = s.FullName,
                admissionNo = s.AdmissionNo,
                className = s.ClassName,
                sectionName = s.SectionName,
                batchName = s.BatchName
            }));
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult CreateAttendanceBatch([FromBody] CreateAttendanceBatchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.BatchName) || request?.StudentIds == null || !request.StudentIds.Any())
                return Json(new { success = false, message = "Batch name and students are required." });

            var batchId = batchSvc.CreateBatch(request.BatchName, request.StudentIds);
            return Json(new { success = true, message = $"Batch '{request.BatchName}' created with {request.StudentIds.Count} students.", batchId = batchId });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}

public class CreateAttendanceBatchRequest
{
    public string BatchName { get; set; }
    public List<int> StudentIds { get; set; }
}
