using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/dashboard")]
[ApiRequireStaff]
public class DashboardApiController(LookupService lookup, AttendanceService attSvc, FeesService feesSvc, SchoolMS.Repository.ActivityRepo activityRepo, BirthdayService birthdays) : ControllerBase
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

    /// <summary>Students and faculty whose birthday is today (admin dashboard). The message is the "Birthday" template from Masters.</summary>
    [HttpGet("birthdays-today")]
    public IActionResult BirthdaysToday()
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        return Ok(birthdays.TodayForStaff().Select(b => new {
            kind = b.Kind, id = b.Id, name = b.Name, line = b.Line, age = b.Age,
            fatherPhone = b.FatherPhone, motherPhone = b.MotherPhone, phone = b.Phone, message = b.Message }));
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
        // Which of them already have the "Absent" notification in the parent's login (sent when attendance was saved).
        Dictionary<int, DateTime> notified;
        try { notified = activityRepo.GetAttendanceNotified(SchoolMS.Domain.ActivityType.Absent, DateTime.Today); }
        catch { notified = new(); }
        return Ok(absent.Select(a => new {
            notified = notified.ContainsKey(a.StudentId),
            notifiedAt = notified.TryGetValue(a.StudentId, out var at) ? at.ToString("h:mm tt") : "",
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
