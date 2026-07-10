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
        ViewBag.TotalAdditionalCharges = feesSvc.GetTotalAdditionalCharges();

        return View(stats);
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
