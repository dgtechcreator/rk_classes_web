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

            // Get all active students (paginate in chunks)
            var (students, _) = studentSvc.GetAll(1, 5000, null, null, null, null, yearId, "Active");

            if (!students.Any())
            {
                ViewBag.TotalStudents = 0;
                ViewBag.TotalFees = 0;
                ViewBag.TotalCollected = 0;
                ViewBag.TotalDiscount = 0;
                ViewBag.TotalBalance = 0;
                ViewBag.CollectionPercentage = 0;
                return View();
            }

            // Calculate totals
            decimal totalFees = 0;
            decimal totalCollected = 0;
            decimal totalDiscount = 0;
            decimal totalBalance = 0;

            foreach (var student in students)
            {
                try
                {
                    var feeStructures = feeStructureSvc.GetAllForStudent(student.StudentId);
                    decimal studentTotalFees = feeStructures?.Sum(x => x.Amount) ?? 0;
                    totalFees += studentTotalFees;

                    var history = feesSvc.GetStudentHistory(student.StudentId);
                    decimal studentCollected = history?.Sum(x => x.NetAmount) ?? 0;
                    decimal studentDiscount = history?.Sum(x => x.Discount) ?? 0;

                    totalCollected += studentCollected;
                    totalDiscount += studentDiscount;

                    decimal studentBalance = Math.Max(0, studentTotalFees - studentCollected);
                    totalBalance += studentBalance;
                }
                catch
                {
                    // Skip student if there's an error fetching their data
                    continue;
                }
            }

            ViewBag.TotalStudents = students.Count;
            ViewBag.TotalFees = totalFees;
            ViewBag.TotalCollected = totalCollected;
            ViewBag.TotalDiscount = totalDiscount;
            ViewBag.TotalBalance = totalBalance;
            ViewBag.CollectionPercentage = totalFees > 0 ? Math.Round((totalCollected / totalFees) * 100, 2) : 0;

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
