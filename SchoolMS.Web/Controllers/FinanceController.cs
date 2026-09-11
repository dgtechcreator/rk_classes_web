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
            (int TotalStudents, decimal TotalFees, decimal TotalCollected, decimal TotalDiscount, decimal TotalBalance, decimal CollectionPercentage) summary
                = feesSvc.GetFinanceDashboardSummary(yearId);

            ViewBag.TotalStudents = summary.TotalStudents;
            ViewBag.TotalFees = summary.TotalFees;
            ViewBag.TotalCollected = summary.TotalCollected;
            ViewBag.TotalDiscount = summary.TotalDiscount;
            ViewBag.TotalBalance = summary.TotalBalance;
            ViewBag.CollectionPercentage = summary.CollectionPercentage;

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
