using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class DashboardController(LookupService lookup, AttendanceService attSvc, FeesService feesSvc) : Controller
{
    public IActionResult Index()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1)
            return RedirectToAction("AccessDenied", "Home");
        var stats = lookup.GetDashStats();
        var currentYear = lookup.GetYears().FirstOrDefault(y => y.IsCurrent);
        ViewBag.CurrentYearName = currentYear?.YearName ?? "N/A";

        var overallFeesSummary = feesSvc.GetOverallFeesSummary();
        ViewBag.OverallFeesSummary = overallFeesSummary;

        return View(stats);
    }

    [HttpGet]
    public IActionResult GetPresentStudents()
    {
        var present = attSvc.GetPresentStudentsToday();
        return Json(present.Select(a => new
        {
            studentId = a.StudentId,
            fullName = a.FullName,
            admissionNo = a.AdmissionNo,
            className = a.ClassName,
            sectionName = a.SectionName,
            batchName = a.BatchName,
            medium = a.Medium,
        }));
    }

    // Payments behind the "Fees This Month" card — same rule as the card (payment date in this month,
    // deleted receipts excluded). Admin only, like the dashboard itself.
    [HttpGet]
    public IActionResult FeesThisMonth()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1)
            return RedirectToAction("AccessDenied", "Home");
        var payments = feesSvc.GetPaymentsThisMonth();
        return View(payments);
    }

    [HttpGet]
    public IActionResult GetAbsentStudents()
    {
        var absent = attSvc.GetAbsentStudentsToday();
        return Json(absent.Select(a => new
        {
            studentId = a.StudentId,
            fullName = a.FullName,
            admissionNo = a.AdmissionNo,
            className = a.ClassName,
            sectionName = a.SectionName,
            batchName = a.BatchName,
            medium = a.Medium,
            phone = a.Phone,
            fatherPhone = a.FatherPhone,
            motherPhone = a.MotherPhone
        }));
    }
}
