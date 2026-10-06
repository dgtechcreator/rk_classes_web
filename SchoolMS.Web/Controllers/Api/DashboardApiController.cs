using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/dashboard")]
[ApiRequireStaff]
public class DashboardApiController(LookupService lookup, AttendanceService attSvc, FeesService feesSvc) : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });

        var stats = lookup.GetDashStats();
        var currentYear = lookup.GetYears().FirstOrDefault(y => y.IsCurrent);
        var overallFeesSummary = feesSvc.GetOverallFeesSummary();

        return Ok(new {
            stats,
            currentYearName = currentYear?.YearName ?? "N/A",
            overallFeesSummary,
        });
    }

    [HttpGet("present-today")]
    public IActionResult PresentToday()
    {
        var present = attSvc.GetPresentStudentsToday();
        return Ok(present.Select(a => new {
            studentId = a.StudentId,
            fullName = a.FullName,
            admissionNo = a.AdmissionNo,
            className = a.ClassName,
            sectionName = a.SectionName,
            batchName = a.BatchName,
            medium = a.Medium,
            phone = a.Phone,
            fatherPhone = a.FatherPhone,
            motherPhone = a.MotherPhone,
        }));
    }

    // Payments behind the "Fees This Month" dashboard card — money data, so admin only like the card itself.
    [HttpGet("fees-this-month")]
    public IActionResult FeesThisMonth()
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        var payments = feesSvc.GetPaymentsThisMonth();
        return Ok(new { payments, total = payments.Sum(p => p.NetAmount), count = payments.Count });
    }

    [HttpGet("absent-today")]
    public IActionResult AbsentToday()
    {
        // Named ValueTuple element names are compile-time-only and don't survive into JSON via
        // System.Text.Json reflection, so project explicitly (mirrors DashboardController.GetAbsentStudents).
        var absent = attSvc.GetAbsentStudentsToday();
        return Ok(absent.Select(a => new {
            studentId = a.StudentId,
            fullName = a.FullName,
            admissionNo = a.AdmissionNo,
            className = a.ClassName,
            sectionName = a.SectionName,
            batchName = a.BatchName,
            medium = a.Medium,
            phone = a.Phone,
            fatherPhone = a.FatherPhone,
            motherPhone = a.MotherPhone,
        }));
    }
}
