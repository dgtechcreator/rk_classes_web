using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireFinanceAdmin]
public class FinanceController(FeesService feesSvc, StudentService studentSvc,
    LookupService lookupSvc, FeeStructureService feeStructureSvc) : Controller
{
    [HttpGet]
    public IActionResult Dashboard()
    {
        try
        {
            var yearId = lookupSvc.GetCurrentYearId() ?? 1;

            // Single SP call for all summary data (instant load!)
            var (totalStudents, totalFees, totalCollected, totalDiscount, totalBalance, collectionPercentage)
                = feesSvc.GetFinanceDashboardSummary(yearId);

            ViewBag.TotalStudents = totalStudents;
            ViewBag.TotalFees = totalFees;
            ViewBag.TotalCollected = totalCollected;
            ViewBag.TotalDiscount = totalDiscount;
            ViewBag.TotalBalance = totalBalance;
            ViewBag.CollectionPercentage = collectionPercentage;

            // Get academic breakdown from same SP (already run)
            var academicData = feesSvc.GetFinanceDashboardAcademic(yearId);
            ViewBag.AcademicData = academicData;

            return View();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Finance Dashboard Error: {ex.Message}");
            ViewBag.Error = $"Error loading dashboard: {ex.Message}";
            return View();
        }
    }
}
