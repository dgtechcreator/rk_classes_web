using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequirePermission("finance_view")]
public class FinanceController(FeesService feesSvc, StudentService studentSvc,
    LookupService lookupSvc, FeeStructureService feeStructureSvc, ActivityNotifier notify) : Controller
{
    /// <summary>One click for the whole list: every given student who still owes fees gets a "Fee reminder" notification in the parent
    /// login (same rule as the Finance list, and the same as the mobile app's button). The balance is read here, never trusted from the page.</summary>
    [HttpPost]
    public IActionResult RemindDue([FromBody] List<int> ids)
    {
        var set = (ids ?? new()).Distinct().Take(2000).ToHashSet();
        if (set.Count == 0) return Json(new { success = false, message = "No students selected." });
        var rows = feesSvc.GetFinanceStudents(null, null, null, true).Where(r => set.Contains(r.StudentId)).ToList();
        int sent = 0;
        foreach (var r in rows)
        {
            if (r.TotalFees <= 0 || r.Balance < 1) continue;
            notify.FeeDue(r.StudentId, r.Balance, DateTime.Today);
            sent++;
        }
        return Json(new { success = true, sent, message = $"Fee reminder sent to {sent} parent login(s)." });
    }

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
            // Total = what the institute will really receive (fee after discount); the discount is shown only as information.
            var netFees = summary.TotalFees - summary.TotalDiscount;
            ViewBag.TotalFees = netFees;
            ViewBag.TotalCollected = summary.TotalCollected;
            ViewBag.TotalDiscount = summary.TotalDiscount;
            ViewBag.TotalBalance = summary.TotalBalance;
            ViewBag.CollectionPercentage = netFees <= 0 ? 0m : Math.Round(Math.Min(100m, summary.TotalCollected / netFees * 100m), 1);

            // Get academic breakdown
            // Class + batch + medium rows (same as the mobile app); falls back to the plain class + batch rows.
            List<SchoolMS.Domain.FinanceAcademicBreakdown> academicData;
            try { academicData = feesSvc.GetFinanceGroups(); if (academicData.Count == 0) academicData = feesSvc.GetFinanceDashboardAcademic(yearId); }
            catch { academicData = feesSvc.GetFinanceDashboardAcademic(yearId); }
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

    // Student-wise fee position behind the dashboard tiles / class rows. Same rule as the dashboard totals
    // (fees - collected - discount), so the rows add up to the tile that was clicked.
    [HttpGet]
    public IActionResult Students(string? className = null, string? batchName = null, string view = "all", string? sectionName = null, bool anyBatch = false, bool noMedium = false)
    {
        var rows = feesSvc.GetFinanceStudents(className, batchName, sectionName, anyBatch);
        if (noMedium) rows = rows.Where(r => string.IsNullOrWhiteSpace(r.SectionName)).ToList();   // the row of students with no medium set
        view = (view ?? "all").ToLowerInvariant();
        rows = view switch
        {
            "fees"      => rows.OrderByDescending(r => r.TotalFees - r.Discount).ToList(),
            "collected" => rows.OrderByDescending(r => r.Collected).ToList(),
            "due"       => rows.Where(r => r.Balance > 0.5m).OrderByDescending(r => r.Balance).ToList(),
            _           => rows.OrderBy(r => r.FullName).ToList(),
        };
        ViewBag.ClassName = className;
        ViewBag.BatchName = batchName;
        ViewBag.View = view;
        ViewBag.Title2 = !string.IsNullOrWhiteSpace(className)
            ? string.Join(" · ", new[] { className!.Trim(), sectionName?.Trim(), batchName?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)))
            : view switch { "fees" => "Total Fees", "collected" => "Collected Fees", "due" => "Pending Dues", _ => "All Students" };
        return View(rows);
    }

    // Payments of one student (for the Receipt button in the list above).
    [HttpGet]
    public IActionResult StudentPayments(int id)
    {
        var list = feesSvc.GetStudentHistory(id);
        return Json(list.Select(p => new {
            paymentId = p.PaymentId, receiptNo = p.ReceiptNo,
            date = p.PaymentDate.ToString("dd-MM-yyyy"), mode = p.PaymentMode, amount = p.NetAmount
        }));
    }

    [HttpGet]
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

            ViewBag.ClassName = className;
            ViewBag.BatchName = batchName;
            ViewBag.TotalStudents = details.Count;
            ViewBag.TotalFees = totalFees;
            ViewBag.TotalCollected = totalCollected;
            ViewBag.TotalDiscount = totalDiscount;
            ViewBag.TotalBalance = totalFees - totalCollected - totalDiscount;
            ViewBag.StudentDetails = details;

            return View();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Class Detail Error: {ex.Message}");
            ViewBag.Error = $"Error loading class details: {ex.Message}";
            return View();
        }
    }

    [HttpGet]
    public IActionResult ExportAcademicBreakdown()
    {
        try
        {
            var yearId = lookupSvc.GetCurrentYearId() ?? 1;
            List<SchoolMS.Domain.FinanceAcademicBreakdown> academicData;
            try { academicData = feesSvc.GetFinanceGroups(); if (academicData.Count == 0) academicData = feesSvc.GetFinanceDashboardAcademic(yearId); }
            catch { academicData = feesSvc.GetFinanceDashboardAcademic(yearId); }

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Standard,Batch,Medium,No. of Students,Total Fees (after discount),Discount (info),Collected,Balance");

            foreach (var row in academicData)
            {
                var balance = row.TotalFees - row.EstimatedCollected - row.EstimatedDiscount;
                csv.AppendLine($"{row.ClassName},{row.BatchName},{row.SectionName},{row.StudentCount},{(long)(row.TotalFees - row.EstimatedDiscount)},{(long)row.EstimatedDiscount},{(long)row.EstimatedCollected},{(long)balance}");
            }

            var fileName = $"AcademicBreakdown_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export Error: {ex.Message}");
            return BadRequest("Error exporting data");
        }
    }

    [HttpGet]
    public IActionResult ExportClassStudents(string className, string batchName)
    {
        try
        {
            var details = feesSvc.GetClassStudentFeeDetails(className, batchName);

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Student Name,Total Fees,Discount,Collected,Balance");

            foreach (var student in details)
            {
                csv.AppendLine($"{student.StudentName},{(long)student.TotalFees},{(long)student.Discount},{(long)student.Collected},{(long)student.Balance}");
            }

            var fileName = $"{className}_{batchName}_StudentDetails_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export Error: {ex.Message}");
            return BadRequest("Error exporting data");
        }
    }
}
