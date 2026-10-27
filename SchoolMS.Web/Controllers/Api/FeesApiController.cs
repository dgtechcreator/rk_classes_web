using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/fees")]
[ApiRequireStaff]
public class FeesApiController(FeesService svc, LookupService lookup,
    ClassFeeSetupService feeSetupSvc, StudentService studentSvc, FeeStructureService feeStructureSvc, FeePositionService feePos) : ControllerBase
{
    public record SavePayReq(int StudentId, decimal PayingNow, decimal AdditionalDiscount,
        decimal AdditionalCharges, DateTime? PaymentDate, DateTime? DueDate,
        string PaymentMode, string? TransactionRef, string? Remarks);

    public record PreviousDuesReq(int StudentId, decimal Amount, string? Note);

    public record SavePaymentReq(int StudentId, int FeeTypeId, decimal Amount, decimal Discount,
        decimal LateFine, string PaymentMode, string? TransactionRef, string? Month, string? Remarks, int? AcademicYearId);

    [HttpGet]
    [ApiRequirePermission("fee_collection")]
    public IActionResult Index(int? yearId, int? sectionId, int? classId, bool search = false)
    {
        yearId ??= lookup.GetCurrentYearId();

        var students = new List<Student>();
        if (search)
        {
            var (data, _) = studentSvc.GetAll(1, 500, null, classId, sectionId, null, yearId, "Active");
            students = data;
        }

        var feeStructures = classId.HasValue
            ? feeStructureSvc.GetAll(yearId, classId, sectionId)
            : new List<FeeStructure>();
        if (!feeStructures.Any() && classId.HasValue && sectionId.HasValue)
            feeStructures = feeStructureSvc.GetAll(yearId, classId, null);
        if (!feeStructures.Any() && classId.HasValue)
            feeStructures = feeStructureSvc.GetAll(null, classId, sectionId);
        if (!feeStructures.Any() && classId.HasValue)
            feeStructures = feeStructureSvc.GetAll(null, classId, null);

        return Ok(new { students, feeStructures, yearId, classId, sectionId, search });
    }

    [HttpGet("pay/{id:int}")]
    [ApiRequirePermission("fee_collection")]
    public IActionResult Pay(int id, int? paymentId)
    {
        var student = studentSvc.GetById(id);
        if (student == null) return NotFound();

        var fee = feePos.Calculate(student);
        var structures = fee.Structures;
        var history = fee.History;

        object? existingPayment = null;
        if (paymentId.HasValue)
        {
            var ep = svc.GetPaymentById(paymentId.Value);
            if (ep != null)
                existingPayment = new {
                    paymentDate = ep.PaymentDate, payingNow = ep.NetAmount, additionalDiscount = ep.Discount,
                    paymentMode = ep.PaymentMode, transactionRef = ep.TransactionRef, remarks = ep.Remarks,
                    editPaymentId = paymentId.Value
                };
        }

        return Ok(new {
            student, actualFee = fee.BaseFee, totalPaid = fee.Paid, balance = fee.Balance, dueDate = fee.DueDate,
            additionalCharges = fee.AdditionalCharges, netTotal = fee.NetTotal, previousFees = fee.PreviousYearsFee, previousDue = fee.PreviousYearsDue,
            feeStructures = structures, paymentHistory = history, existingDiscount = fee.Discount,
            agreedDiscount = fee.AgreedDiscount, agreedDiscountReason = student.AgreedDiscountReason,
            paymentDiscount = fee.Discount - fee.AgreedDiscount,   // discount already given on payments (locks the field)
            existingPayment,
            previousDuesEntry = svc.GetPreviousDues(id),
        });
    }

    /// <summary>Sets the dues of earlier years for a student (0 clears). They are added to the student's fee, balance and every Finance total.</summary>
    [HttpPost("previous-dues")]
    [ApiRequirePermission("fee_collection", requireEdit: true)]
    public IActionResult SetPreviousDues([FromBody] PreviousDuesReq req)
    {
        if (studentSvc.GetById(req.StudentId) == null) return NotFound(new { error = "Student not found." });
        var err = svc.SetPreviousDues(req.StudentId, req.Amount, req.Note, User.UserId() ?? 0);
        return err == null ? Ok(new { success = true }) : BadRequest(new { error = err });
    }

    [HttpPost("save-pay")]
    [ApiRequirePermission("fee_collection")]
    public IActionResult SavePay([FromBody] SavePayReq req)
    {
        try
        {
            int uid = User.UserId() ?? 1;
            var student = studentSvc.GetById(req.StudentId);
            if (student == null) return NotFound();

            var paymentDate = req.PaymentDate ?? DateTime.Today;
            var history = svc.GetStudentHistory(req.StudentId);
            decimal existingDiscount = history.Sum(x => x.Discount);
            decimal discountToApply = existingDiscount > 0 ? 0 : req.AdditionalDiscount;

            string finalRemarks = req.Remarks ?? "";
            if (req.AdditionalCharges > 0)
                finalRemarks = (finalRemarks.Length > 0 ? finalRemarks + " | " : "") + AdditionalCharges.Token(req.AdditionalCharges);

            var payId = svc.Collect(req.StudentId, null, req.PayingNow, discountToApply, 0,
                paymentDate, req.PaymentMode, req.TransactionRef,
                student.AcademicYearId, null, finalRemarks, uid, req.DueDate);

            if (payId <= 0)
                return BadRequest(new { error = "Failed to save payment. Please try again." });

            return Ok(new { success = true, paymentId = payId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Error saving payment: " + ex.Message });
        }
    }

    public record SetChargesReq(int StudentId, decimal AdditionalCharges);

    /// Sets the student's TOTAL additional charges (use it to correct a wrong entry); no payment is taken.
    [HttpPost("additional-charges")]
    [ApiRequirePermission("fee_collection")]
    public IActionResult SetAdditionalCharges([FromBody] SetChargesReq req)
    {
        try
        {
            if (studentSvc.GetById(req.StudentId) == null) return NotFound();
            var err = svc.SetAdditionalCharges(req.StudentId, req.AdditionalCharges);
            if (err != null) return BadRequest(new { error = err });
            return Ok(new { success = true, additionalCharges = AdditionalCharges.Total(svc.GetStudentHistory(req.StudentId)) });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Error updating additional charges: " + ex.Message });
        }
    }

    [HttpGet("collect")]
    [ApiRequirePermission("fee_collect")]
    public IActionResult Collect()
    {
        var currentYearId = lookup.GetCurrentYearId();
        return Ok(new {
            students = lookup.GetStudentDropdown(),
            feeTypes = lookup.GetFeeTypes(),
            years = lookup.GetYears(),
            classes = lookup.GetClasses(),
            batches = lookup.GetBatches(),
            sections = lookup.GetSections(),
            academicYearId = currentYearId
        });
    }

    [HttpGet("fee-amount")]
    [ApiRequirePermission("fee_collect")]
    public IActionResult GetFeeAmount(int batchId, int classId, int sectionId)
    {
        var amount = feeSetupSvc.GetAmount(batchId, classId, sectionId);
        return Ok(new { found = amount.HasValue, amount = amount ?? 0 });
    }

    [HttpPost("save-payment")]
    [ApiRequirePermission("fee_collect")]
    public IActionResult SavePayment([FromBody] SavePaymentReq m)
    {
        try
        {
            int uid = User.UserId() ?? 1;
            var payId = svc.Collect(m.StudentId, m.FeeTypeId, m.Amount, m.Discount, m.LateFine,
                DateTime.Today, m.PaymentMode, m.TransactionRef, m.AcademicYearId, m.Month, m.Remarks, uid);

            if (payId <= 0)
                return BadRequest(new { error = "Failed to save payment. Please try again." });

            return Ok(new { success = true, paymentId = payId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Error saving payment: " + ex.Message });
        }
    }

    [HttpGet("summary")]
    [ApiRequirePermission("fee_collection")]
    public IActionResult Summary(int page = 1, string? search = null, string? month = null, int? feeType = null)
    {
        var (data, total) = svc.GetAll(page, 15, search, month, feeType);
        var (allData, _) = svc.GetAll(1, 10000, search, month, feeType);
        decimal grandTotal = allData.Sum(p => p.NetAmount);

        return Ok(new { payments = data, total, grandTotalAmount = grandTotal, page, feeTypes = lookup.GetFeeTypes() });
    }

    [HttpGet("summary-json")]
    [ApiRequirePermission("fee_collection")]
    public IActionResult SummaryJson(int? monthNum = null)
    {
        var (data, _) = svc.GetAll(1, 100, null, null, null);

        if (monthNum.HasValue && monthNum.Value > 0 && monthNum.Value <= 12)
        {
            data = data.Where(p => p.PaymentDate.Month == monthNum.Value &&
                                   p.PaymentDate.Year == DateTime.Today.Year).ToList();
        }

        var result = data.Select(p => new {
            paymentId = p.PaymentId,
            receiptNo = p.ReceiptNo,
            studentName = p.StudentName,
            admissionNo = p.AdmissionNo,
            className = p.ClassName,
            sectionName = p.SectionName,
            batchName = p.BatchName,
            netAmount = p.NetAmount,
            discount = p.Discount,
            paymentMode = p.PaymentMode,
            paymentDate = p.PaymentDate.ToString("dd MMM yyyy"),
            collectorName = p.CollectorName
        }).ToList();
        return Ok(result);
    }

    [HttpGet("receipt/{id:int}")]
    [ApiRequireStaffOrParent]
    public IActionResult Receipt(int id)
        // TODO: PDF receipt generation — Phase 2
        => StatusCode(501, new { error = "Receipt PDF generation not yet available via API." });

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("fee_collection")]
    public IActionResult DeletePayment(int id)
    {
        try
        {
            int uid = User.UserId() ?? 1;
            svc.DeletePayment(id, uid);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Error deleting payment: " + ex.Message });
        }
    }
}
