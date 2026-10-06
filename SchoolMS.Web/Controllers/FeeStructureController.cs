using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class FeeStructureController(FeeStructureService svc, LookupService lookup, FeePositionService feePos) : Controller
{
    public IActionResult Index(int? yearId, int? classId, int? sectionId)
    {
        yearId ??= lookup.GetCurrentYearId();
        var list    = svc.GetAll(yearId, classId, sectionId);
        var summary = svc.GetSummary(yearId);
        if (yearId == lookup.GetCurrentYearId()) feePos.EnrichSummary(summary);
        ViewBag.Years     = lookup.GetYears();
        ViewBag.Classes   = lookup.GetClasses();
        ViewBag.Sections  = lookup.GetSections();
        ViewBag.YearId    = yearId;
        ViewBag.ClassId   = classId;
        ViewBag.SectionId = sectionId;
        ViewBag.Summary   = summary;
        return View(list);
    }

    public IActionResult Create()
    {
        var currentYearId = lookup.GetCurrentYearId();
        ViewBag.Years    = lookup.GetYears();
        ViewBag.Classes  = lookup.GetClasses();
        ViewBag.Sections = lookup.GetSections();
        ViewBag.FeeTypes = lookup.GetFeeTypes();
        return View("Form", new FeeStructure { DueDay=10, IsMonthly=true, AcademicYearId=currentYearId??0 });
    }

    public IActionResult Edit(int id)
    {
        var list = svc.GetAll(null, null, null);
        var fs   = list.FirstOrDefault(x => x.StructureId == id);
        if (fs == null) return NotFound();
        ViewBag.Years    = lookup.GetYears();
        ViewBag.Classes  = lookup.GetClasses();
        ViewBag.Sections = lookup.GetSections();
        ViewBag.FeeTypes = lookup.GetFeeTypes();
        return View("Form", fs);
    }

    [HttpPost]
    public IActionResult Save(FeeStructure model, string? returnTo)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        var newId = svc.Save(model, uid);
        if (newId == -1)
        {
            TempData["Error"] = "A fee structure for this Year / Class / Section / Fee Type already exists.";
            // Return to origin — Masters or FeeStructure
            if (returnTo == "masters")
            { TempData["OpenTab"] = "feesetup"; return RedirectToAction("Index", "Masters"); }
            return RedirectToAction("Create");
        }
        TempData["Success"] = "Fee head saved successfully.";
        if (returnTo == "masters")
        { TempData["OpenTab"] = "feesetup"; return RedirectToAction("Index", "Masters"); }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        svc.Delete(id);
        TempData["Success"] = "Fee structure deleted. Unpaid student fees removed.";
        return RedirectToAction("Index");
    }

    public IActionResult FeesDue(int? yearId, int? classId, int? sectionId,
        int? feeTypeId, string? status, int page = 1)
    {
        yearId ??= lookup.GetCurrentYearId();
        var (data, total) = svc.GetFeesDue(yearId, classId, sectionId, feeTypeId, status, page, 20);
        ViewBag.Years     = lookup.GetYears();
        ViewBag.Classes   = lookup.GetClasses();
        ViewBag.Sections  = lookup.GetSections();
        ViewBag.FeeTypes  = lookup.GetFeeTypes();
        ViewBag.YearId    = yearId;
        ViewBag.ClassId   = classId;
        ViewBag.SectionId = sectionId;
        ViewBag.FeeTypeId = feeTypeId;
        ViewBag.Status    = status;
        ViewBag.Page      = page;
        ViewBag.Total     = total;
        ViewBag.TotalPages= Math.Max(1,(int)Math.Ceiling((double)total/20));
        return View(data);
    }

    [HttpPost]
    public IActionResult CollectFee(int studentFeeId, decimal paidAmount,
        decimal discount, decimal lateFine, string paymentMode,
        string? transactionRef, string? remarks)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        svc.CollectFee(studentFeeId, paidAmount, discount, lateFine, paymentMode, transactionRef, remarks, uid);
        TempData["Success"] = "Fee collected and receipt generated.";
        return RedirectToAction("FeesDue");
    }

    [HttpPost]
    public IActionResult GenerateMonthly(string month, int yearId)
    {
        int count = svc.GenerateMonthlyFees(month, yearId);
        TempData["Success"] = $"Generated fees for {month}. Records created: {count}";
        return RedirectToAction("FeesDue");
    }
}
