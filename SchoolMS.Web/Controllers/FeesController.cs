using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class FeesController(FeesService svc, LookupService lookup, ClassFeeSetupService feeSetupSvc) : Controller
{
    public IActionResult Index(int page=1, string? search=null, string? month=null, int? feeType=null)
    {
        var (data, total) = svc.GetAll(page, 15, search, month, feeType);
        return View(new FeesListVM {
            Payments=data, Total=total, Page=page, Search=search,
            MonthFilter=month, FeeTypeFilter=feeType, FeeTypes=lookup.GetFeeTypes()
        });
    }

    public IActionResult Collect()
    {
        var currentYearId = lookup.GetCurrentYearId();
        return View(new FeeCollectVM {
            Students=lookup.GetStudentDropdown(),
            FeeTypes=lookup.GetFeeTypes(),
            Years=lookup.GetYears(),
            Classes=lookup.GetClasses(),
            Batches=lookup.GetBatches(),
            Sections=lookup.GetSections(),
            AcademicYearId=currentYearId
        });
    }

    public IActionResult GetFeeAmount(int batchId, int classId, int sectionId)
    {
        var amount = feeSetupSvc.GetAmount(batchId, classId, sectionId);
        return Json(new { found = amount.HasValue, amount = amount ?? 0 });
    }

    [HttpPost]
    public IActionResult SavePayment(FeeCollectVM m)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        var payId = svc.Collect(m.StudentId, m.FeeTypeId, m.Amount, m.Discount, m.LateFine,
            DateTime.Today, m.PaymentMode, m.TransactionRef, m.AcademicYearId, m.Month, m.Remarks, uid);
        TempData["Success"] = "Fee collected successfully.";
        return RedirectToAction("Receipt", new { id = payId });
    }

    public IActionResult Receipt(int id)
    {
        var payment = svc.GetPaymentById(id);
        if (payment == null) return NotFound();
        return View(payment);
    }
}
