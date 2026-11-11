using Microsoft.AspNetCore.Mvc;
using SchoolMS.Repository;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireTeacherPaymentAccess]
public class TeacherPaymentController(TeacherPaymentService paymentSvc, FacultyService facultySvc, TeacherAccountLinker linker) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var unpaidPayments = paymentSvc.GetUnpaid();
            var monthlyPayments = paymentSvc.GetByMonth(currentMonth, currentYear);

            ViewBag.UnpaidPayments = unpaidPayments;
            ViewBag.MonthlyPayments = monthlyPayments;
            ViewBag.CurrentMonth = currentMonth;
            ViewBag.CurrentYear = currentYear;
            ViewBag.AllFaculty = facultySvc.GetAllActive();

            return View();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Teacher Payment Error: {ex.Message}");
            ViewBag.Error = $"Error loading page: {ex.Message}";
            return View();
        }
    }

    [HttpPost]
    public IActionResult SavePayment(int facultyId, string paymentType, decimal rate, decimal? quantity,
        int paymentMonth, int paymentYear, string? remarks)
    {
        try
        {
            var userId = HttpContext.Session.GetUserId() ?? 0;
            var paymentId = paymentSvc.Save(facultyId, paymentType, rate, quantity, paymentMonth, paymentYear, remarks, userId);

            return Json(new { success = true, paymentId, message = "Payment entry saved successfully" });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save Payment Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult MarkAsPaid(int paymentId, string? paymentMode, string? transactionRef)
    {
        try
        {
            var userId = HttpContext.Session.GetUserId() ?? 0;
            paymentSvc.MarkAsPaid(paymentId, paymentMode, transactionRef, userId);

            var payment = paymentSvc.GetById(paymentId);

            return Json(new
            {
                success = true,
                message = "Payment marked as paid",
                payment = new
                {
                    paymentId = payment?.TeacherPaymentId,
                    receiptNo = payment?.ReceiptNo,
                    facultyName = payment?.FacultyName,
                    totalAmount = payment?.TotalAmount,
                    paymentDate = payment?.PaymentDate?.ToString("dd-MMM-yyyy")
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mark as Paid Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Edit one payment entry (pending or paid). [returnUrl] = where to go back to (the Expenses list also links here).</summary>
    [HttpGet]
    public IActionResult Edit(int id, string? returnUrl = null)
    {
        var p = paymentSvc.GetById(id);
        if (p == null) { TempData["Error"] = "This payment no longer exists."; return RedirectToAction("Summary"); }
        ViewBag.ReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/TeacherPayment/Summary";
        return View(p);
    }

    public record UpdateReq(int PaymentId, string PaymentType, decimal Rate, decimal? Quantity, int PaymentMonth, int PaymentYear,
        string? Remarks, string? PaymentMode, string? TransactionRef, DateTime? PaymentDate);

    [HttpPost]
    public IActionResult UpdatePayment([FromBody] UpdateReq req)
    {
        var err = paymentSvc.Update(req.PaymentId, req.PaymentType, req.Rate, req.Quantity, req.PaymentMonth, req.PaymentYear,
            req.Remarks, req.PaymentMode, req.TransactionRef, req.PaymentDate, HttpContext.Session.GetUserId() ?? 0);
        return Json(new { success = err == null, message = err ?? "Payment updated." });
    }

    [HttpPost]
    public IActionResult DeletePayment(int paymentId)
    {
        try
        {
            var userId = HttpContext.Session.GetUserId() ?? 0;
            paymentSvc.Delete(paymentId, userId);

            return Json(new { success = true, message = "Payment deleted successfully" });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Delete Payment Error: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult Receipt(int paymentId)
    {
        try
        {
            var payment = paymentSvc.GetById(paymentId);
            if (payment == null)
            {
                return NotFound("Payment not found");
            }

            ViewBag.Payment = payment;
            return View();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Receipt Error: {ex.Message}");
            return NotFound("Error loading receipt");
        }
    }

    /// <summary>The filtered summary as a file Excel opens (CSV, UTF-8 with BOM): one row per payment + totals. Same filters as the Summary page.</summary>
    [HttpGet]
    public IActionResult ExportSummary(int? year = null, int month = 0, int? facultyId = null, string? status = null)
    {
        var y = year ?? DateTime.Now.Year;
        var list = paymentSvc.Search(y, month > 0 ? month : null, facultyId, status);
        var sum = paymentSvc.Summarize(list);
        string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        var monthNames = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.MonthNames;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Teacher Payment Summary,{y}{(month > 0 ? " " + monthNames[month - 1] : "")},{(string.IsNullOrWhiteSpace(status) ? "all" : status)}");
        sb.AppendLine("Teacher,For Month,Year,Type,Hours/Topics,Rate,Amount,Status,Paid On,Mode,Reference,Receipt No,Remarks");
        foreach (var p in list.OrderBy(p => p.FacultyName).ThenBy(p => p.PaymentYear).ThenBy(p => p.PaymentMonth))
            sb.AppendLine(string.Join(",", Q(p.FacultyName?.Trim()), monthNames[Math.Clamp(p.PaymentMonth, 1, 12) - 1], p.PaymentYear,
                p.PaymentType == "Fixed" ? "Fixed Salary" : p.PaymentType, p.PaymentType == "Fixed" ? "" : (p.Quantity?.ToString("0.##") ?? ""),
                p.Rate.ToString("0.##"), p.TotalAmount.ToString("0.##"), p.IsPaid ? "Paid" : "Pending",
                p.IsPaid && p.PaymentDate.HasValue ? p.PaymentDate.Value.ToString("dd-MM-yyyy") : "",
                Q(p.PaymentMode), Q(p.TransactionRef), Q(p.ReceiptNo), Q(p.Remarks)));
        sb.AppendLine();
        sb.AppendLine($"Total,,,,,,{sum.TotalAmount:0.##}");
        sb.AppendLine($"Paid ({sum.PaidCount}),,,,,,{sum.PaidAmount:0.##}");
        sb.AppendLine($"Pending ({sum.PendingCount}),,,,,,{sum.PendingAmount:0.##}");
        sb.AppendLine();
        sb.AppendLine("By teacher,Entries,Paid,Pending");
        foreach (var t in sum.Teachers)
            sb.AppendLine($"{Q(t.FacultyName)},{t.Count},{t.PaidAmount:0.##},{t.PendingAmount:0.##}");
        var bytes = new System.Text.UTF8Encoding(true).GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"TeacherPayments_{y}{(month > 0 ? "_" + month.ToString("00") : "")}_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    // Filtered summary (admin / accountant): year, month (0 = whole year), teacher, status.
    [HttpGet]
    public IActionResult Summary(int? year = null, int month = 0, int? facultyId = null, string? status = null)
    {
        try
        {
            var y = year ?? DateTime.Now.Year;
            var list = paymentSvc.Search(y, month > 0 ? month : null, facultyId, status);
            var years = paymentSvc.Search(null, null, null, null).Select(p => p.PaymentYear).Append(DateTime.Now.Year).Distinct().OrderByDescending(v => v).ToList();

            ViewBag.Year = y; ViewBag.Month = month; ViewBag.FacultyId = facultyId; ViewBag.Status = (status ?? "all").ToLowerInvariant();
            ViewBag.Years = years;
            ViewBag.AllFaculty = facultySvc.GetAllActive();
            ViewBag.Summary = paymentSvc.Summarize(list);
            ViewBag.Payments = list;
            return View();
        }
        catch (Exception ex)
        {
            ViewBag.Error = $"Error loading page: {ex.Message}";
            ViewBag.Years = new List<int> { DateTime.Now.Year };
            ViewBag.AllFaculty = new List<SchoolMS.Domain.Faculty>();
            ViewBag.Summary = new SchoolMS.Domain.TeacherPaymentSummary();
            ViewBag.Payments = new List<SchoolMS.Domain.TeacherPayment>();
            ViewBag.Year = DateTime.Now.Year; ViewBag.Month = 0; ViewBag.Status = "all";
            return View();
        }
    }
}
