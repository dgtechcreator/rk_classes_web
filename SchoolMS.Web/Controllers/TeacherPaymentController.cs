using Microsoft.AspNetCore.Mvc;
using SchoolMS.Repository;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireTeacherPaymentAccess]
public class TeacherPaymentController(TeacherPaymentService paymentSvc, StudentService studentSvc) : Controller
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
            ViewBag.AllFaculty = studentSvc.GetAllFaculty();

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
}
