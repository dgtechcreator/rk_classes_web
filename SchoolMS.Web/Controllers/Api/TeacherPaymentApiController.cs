using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/teacher-payment")]
[ApiRequireTeacherPaymentAccess]
public class TeacherPaymentApiController(TeacherPaymentService paymentSvc, FacultyService facultySvc, TeacherAccountLinker linker) : ControllerBase
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

    public record UpdatePaymentReq(int PaymentId, string PaymentType, decimal Rate, decimal? Quantity, int PaymentMonth, int PaymentYear,
        string? Remarks, string? PaymentMode, string? TransactionRef, DateTime? PaymentDate);

    /// <summary>Edit an entry (pending or paid); the total is recalculated.</summary>
    /// <summary>One entry (used by the app to edit a salary row of the Expenses list).</summary>
    [HttpGet("{paymentId:int}")]
    public IActionResult GetOne(int paymentId)
    {
        var p = paymentSvc.GetById(paymentId);
        return p == null ? NotFound(new { error = "Payment not found." }) : Ok(p);
    }

    [HttpPost("update")]
    public IActionResult UpdatePayment([FromBody] UpdatePaymentReq req)
    {
        var err = paymentSvc.Update(req.PaymentId, req.PaymentType, req.Rate, req.Quantity, req.PaymentMonth, req.PaymentYear,
            req.Remarks, req.PaymentMode, req.TransactionRef, req.PaymentDate, User.UserId() ?? 0);
        return Ok(new { success = err == null, message = err ?? "Payment updated." });
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

    /// Admin / accountant summary with filters: year, month (0 = whole year), facultyId, status (all|paid|pending).
    [HttpGet("summary")]
    public IActionResult Summary(int? year = null, int month = 0, int? facultyId = null, string? status = null)
    {
        try
        {
            // year omitted = current year; year <= 0 = every year (the app loads everything once and filters locally)
            int? y = year.HasValue ? (year.Value > 0 ? year.Value : null) : DateTime.Now.Year;
            var list = paymentSvc.Search(y, month > 0 ? month : null, facultyId, status);
            var yearsAvailable = paymentSvc.Search(null, null, null, null).Select(p => p.PaymentYear).Distinct().ToList();
            if (!yearsAvailable.Contains(DateTime.Now.Year)) yearsAvailable.Add(DateTime.Now.Year);

            return Ok(new {
                year = y ?? 0, month, facultyId, status = status ?? "all",
                summary = paymentSvc.Summarize(list),
                payments = list,
                years = yearsAvailable.OrderByDescending(v => v),
                allFaculty = facultySvc.GetAllActive().Select(f => new { f.FacultyId, f.FullName }),
                // kept for older app builds
                monthlyPayments = list, currentMonth = DateTime.Now.Month, currentYear = DateTime.Now.Year,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error loading page: {ex.Message}" });
        }
    }
}
