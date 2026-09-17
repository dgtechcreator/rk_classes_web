using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/teacher-payment")]
[ApiRequireTeacherPaymentAccess]
public class TeacherPaymentApiController(TeacherPaymentService paymentSvc, FacultyService facultySvc) : ControllerBase
{
    public record SavePaymentReq(int FacultyId, string PaymentType, decimal Rate, decimal? Quantity,
        int PaymentMonth, int PaymentYear, string? Remarks);
    public record MarkAsPaidReq(int PaymentId, string? PaymentMode, string? TransactionRef);

    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var unpaidPayments = paymentSvc.GetUnpaid();
            var monthlyPayments = paymentSvc.GetByMonth(currentMonth, currentYear);

            return Ok(new {
                unpaidPayments, monthlyPayments, currentMonth, currentYear,
                allFaculty = facultySvc.GetAllActive(),
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error loading page: {ex.Message}" });
        }
    }

    [HttpPost("save")]
    public IActionResult SavePayment([FromBody] SavePaymentReq req)
    {
        try
        {
            var userId = User.UserId() ?? 0;
            var paymentId = paymentSvc.Save(req.FacultyId, req.PaymentType, req.Rate, req.Quantity,
                req.PaymentMonth, req.PaymentYear, req.Remarks, userId);

            return Ok(new { success = true, paymentId, message = "Payment entry saved successfully" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("mark-paid")]
    public IActionResult MarkAsPaid([FromBody] MarkAsPaidReq req)
    {
        try
        {
            var userId = User.UserId() ?? 0;
            paymentSvc.MarkAsPaid(req.PaymentId, req.PaymentMode, req.TransactionRef, userId);

            var payment = paymentSvc.GetById(req.PaymentId);

            return Ok(new {
                success = true,
                message = "Payment marked as paid",
                payment = new {
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
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{paymentId:int}/delete")]
    public IActionResult DeletePayment(int paymentId)
    {
        try
        {
            var userId = User.UserId() ?? 0;
            paymentSvc.Delete(paymentId, userId);

            return Ok(new { success = true, message = "Payment deleted successfully" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    // Receipt: PDF generation — Phase 2
    [HttpGet("receipt/{paymentId:int}")]
    public IActionResult Receipt(int paymentId)
        => StatusCode(501, new { error = "Receipt PDF generation not yet available via API." });

    [HttpGet("summary")]
    public IActionResult Summary()
    {
        try
        {
            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var monthlyPayments = paymentSvc.GetByMonth(currentMonth, currentYear);

            return Ok(new { monthlyPayments, currentMonth, currentYear });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error loading page: {ex.Message}" });
        }
    }
}
