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
        var yearId = lookupSvc.GetCurrentYearId();

        // Get all students
        var (students, _) = studentSvc.GetAll(1, 10000, null, null, null, null, yearId, "Active");

        // Calculate totals
        decimal totalFees = 0;
        decimal totalCollected = 0;
        decimal totalDiscount = 0;
        decimal totalBalance = 0;

        foreach (var student in students)
        {
            var feeStructures = feeStructureSvc.GetAllForStudent(student.StudentId);
            decimal studentTotalFees = feeStructures.Sum(x => x.Amount);
            totalFees += studentTotalFees;

            var history = feesSvc.GetStudentHistory(student.StudentId);
            decimal studentCollected = history.Sum(x => x.NetAmount);
            decimal studentDiscount = history.Sum(x => x.Discount);

            totalCollected += studentCollected;
            totalDiscount += studentDiscount;

            decimal studentBalance = Math.Max(0, studentTotalFees - studentCollected);
            totalBalance += studentBalance;
        }

        ViewBag.TotalStudents = students.Count;
        ViewBag.TotalFees = totalFees;
        ViewBag.TotalCollected = totalCollected;
        ViewBag.TotalDiscount = totalDiscount;
        ViewBag.TotalBalance = totalBalance;
        ViewBag.CollectionPercentage = totalFees > 0 ? Math.Round((totalCollected / totalFees) * 100, 2) : 0;

        return View();
    }
}
