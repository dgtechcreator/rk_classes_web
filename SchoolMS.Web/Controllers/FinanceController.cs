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

            // Get all active students - limited query
            var (students, _) = studentSvc.GetAll(1, 2000, null, null, null, null, yearId, "Active");
            int totalStudents = students.Count;

            if (totalStudents == 0)
            {
                ViewBag.TotalStudents = 0;
                ViewBag.TotalFees = 0;
                ViewBag.TotalCollected = 0;
                ViewBag.TotalDiscount = 0;
                ViewBag.TotalBalance = 0;
                ViewBag.CollectionPercentage = 0;
                return View();
            }

            // Get ALL fee structures once (instead of per-student)
            var allFeeStructures = feeStructureSvc.GetAll(yearId, null, null);
            var feeStructuresByStudent = students.ToDictionary(
                s => s.StudentId,
                s => allFeeStructures.Where(f =>
                    (f.ClassId == s.ClassId) &&
                    (f.SectionId == null || f.SectionId == s.SectionId)
                ).ToList()
            );

            // Get ALL fee payment history at once (if possible via bulk method)
            decimal totalFees = 0;
            decimal totalCollected = 0;
            decimal totalDiscount = 0;
            decimal totalBalance = 0;

            // Calculate totals from pre-fetched data
            foreach (var student in students)
            {
                try
                {
                    // Use pre-fetched structures
                    var feeStructures = feeStructuresByStudent.ContainsKey(student.StudentId)
                        ? feeStructuresByStudent[student.StudentId]
                        : new List<SchoolMS.Domain.FeeStructure>();

                    decimal studentTotalFees = feeStructures.Sum(x => x.Amount);
                    totalFees += studentTotalFees;

                    // Still need per-student history (no bulk method available)
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
                    continue;
                }
            }

            ViewBag.TotalStudents = totalStudents;
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
