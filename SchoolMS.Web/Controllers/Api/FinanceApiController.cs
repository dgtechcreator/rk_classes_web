using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/finance")]
[ApiRequirePermission("finance_view")]
public class FinanceApiController(FeesService feesSvc, StudentService studentSvc,
    LookupService lookupSvc, FeeStructureService feeStructureSvc) : ControllerBase
{
    [HttpGet("dashboard")]
    public IActionResult Dashboard()
    {
        try
        {
            var yearId = lookupSvc.GetCurrentYearId() ?? 1;

            // GetFinanceDashboardSummary returns a C# named ValueTuple — project to an anonymous
            // object with real property names, since tuple element names don't survive JSON serialization.
            var summary = feesSvc.GetFinanceDashboardSummary(yearId);
            var academicData = feesSvc.GetFinanceDashboardAcademic(yearId);

            return Ok(new {
                totalStudents = summary.TotalStudents,
                totalFees = summary.TotalFees,
                totalCollected = summary.TotalCollected,
                totalDiscount = summary.TotalDiscount,
                totalBalance = summary.TotalBalance,
                collectionPercentage = summary.CollectionPercentage,
                academicData,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error loading dashboard: {ex.Message}" });
        }
    }

    [HttpGet("class-detail")]
    public IActionResult ClassDetail(string className, string batchName)
    {
        try
        {
            var details = feesSvc.GetClassStudentFeeDetails(className, batchName);

            decimal totalFees = 0, totalCollected = 0, totalDiscount = 0;
            foreach (var detail in details)
            {
                totalFees += detail.TotalFees;
                totalCollected += detail.Collected;
                totalDiscount += detail.Discount;
            }

            return Ok(new {
                className, batchName, totalStudents = details.Count,
                totalFees, totalCollected, totalDiscount,
                totalBalance = totalFees - totalCollected - totalDiscount,
                studentDetails = details,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error loading class details: {ex.Message}" });
        }
    }
}
