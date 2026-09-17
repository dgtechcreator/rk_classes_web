using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/fees")]
[ApiRequireStaff]
public class FeesApiController(FeesService svc, LookupService lookup,
    ClassFeeSetupService feeSetupSvc, StudentService studentSvc, FeeStructureService feeStructureSvc) : ControllerBase
{
    public record SavePayReq(int StudentId, decimal PayingNow, decimal AdditionalDiscount,
        decimal AdditionalCharges, DateTime? PaymentDate, DateTime? DueDate,
        string PaymentMode, string? TransactionRef, string? Remarks);

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

        decimal actualFee = 0;
        DateTime? dueDate = null;
        var structures = feeStructureSvc.GetAllForStudent(id);

        if (!structures.Any() && student.AcademicYearId.HasValue && student.ClassId.HasValue)
        {
            structures = feeStructureSvc.GetAll(student.AcademicYearId, student.ClassId, student.SectionId);
            if (!structures.Any() && student.SectionId.HasValue)
                structures = feeStructureSvc.GetAll(student.AcademicYearId, student.ClassId, null);
            if (!structures.Any())
                structures = feeStructureSvc.GetAll(null, student.ClassId, student.SectionId);
            if (!structures.Any())
                structures = feeStructureSvc.GetAll(null, student.ClassId, null);
        }

        if (structures.Any())
        {
            actualFee = structures.Sum(x => x.Amount);
            var firstDue = structures.FirstOrDefault(x => x.DueDay > 0);
            if (firstDue != null)
                dueDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(firstDue.DueDay - 1);
        }

        var history = svc.GetStudentHistory(id);
        var totalPaid = history.Sum(x => x.NetAmount);
        var totalDiscount = history.Sum(x => x.Discount);

        decimal totalAdditionalCharges = 0;
        foreach (var payment in history)
        {
            if (!string.IsNullOrEmpty(payment.Remarks) && payment.Remarks.Contains("Additional Charges:"))
            {
                var parts = payment.Remarks.Split("|");
                foreach (var part in parts)
                {
                    if (part.Contains("Additional Charges:"))
                    {
                        var chargeStr = part.Replace("Additional Charges:", "").Replace("₹", "").Trim();
                        if (decimal.TryParse(chargeStr, out decimal charge))
                            totalAdditionalCharges += charge;
                    }
                }
            }
        }

        var totalFeeWithAdditional = actualFee + totalAdditionalCharges;
        var netActualFee = totalFeeWithAdditional - totalDiscount;
        var balance = netActualFee - totalPaid;

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
            student, actualFee, totalPaid, balance = Math.Max(0, balance), dueDate,
            feeStructures = structures, paymentHistory = history, existingDiscount = totalDiscount,
            existingPayment,
        });
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
                finalRemarks = (finalRemarks.Length > 0 ? finalRemarks + " | " : "") + $"Additional Charges: ₹{req.AdditionalCharges}";

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
