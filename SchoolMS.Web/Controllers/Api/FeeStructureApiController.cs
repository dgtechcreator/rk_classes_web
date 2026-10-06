using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/fee-structure")]
[ApiRequireStaff]
public class FeeStructureApiController(FeeStructureService svc, LookupService lookup, FeePositionService feePos) : ControllerBase
{
    public record CollectFeeReq(int StudentFeeId, decimal PaidAmount, decimal Discount,
        decimal LateFine, string PaymentMode, string? TransactionRef, string? Remarks);

    [HttpGet]
    [ApiRequirePermission("fee_structure")]
    public IActionResult Index(int? yearId, int? classId, int? sectionId)
    {
        yearId ??= lookup.GetCurrentYearId();
        var list = svc.GetAll(yearId, classId, sectionId);
        var summary = svc.GetSummary(yearId);
        if (yearId == lookup.GetCurrentYearId()) feePos.EnrichSummary(summary);
        return Ok(new { list, summary, yearId, classId, sectionId });
    }

    [HttpGet("create")]
    [ApiRequirePermission("fee_structure")]
    public IActionResult Create()
    {
        var currentYearId = lookup.GetCurrentYearId();
        return Ok(new {
            years = lookup.GetYears(), classes = lookup.GetClasses(), sections = lookup.GetSections(),
            feeTypes = lookup.GetFeeTypes(),
            structure = new FeeStructure { DueDay = 10, IsMonthly = true, AcademicYearId = currentYearId ?? 0 }
        });
    }

    [HttpGet("{id:int}/edit")]
    [ApiRequirePermission("fee_structure")]
    public IActionResult Edit(int id)
    {
        var list = svc.GetAll(null, null, null);
        var fs = list.FirstOrDefault(x => x.StructureId == id);
        if (fs == null) return NotFound();
        return Ok(new {
            years = lookup.GetYears(), classes = lookup.GetClasses(), sections = lookup.GetSections(),
            feeTypes = lookup.GetFeeTypes(), structure = fs
        });
    }

    [HttpPost("save")]
    [ApiRequirePermission("fee_structure")]
    public IActionResult Save([FromBody] FeeStructure model)
    {
        int uid = User.UserId() ?? 1;
        var newId = svc.Save(model, uid);
        if (newId == -1)
            return BadRequest(new { error = "A fee structure for this Year / Class / Section / Fee Type already exists." });
        return Ok(new { structureId = newId });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("fee_structure")]
    public IActionResult Delete(int id)
    {
        svc.Delete(id);
        return Ok(new { success = true });
    }

    [HttpGet("fees-due")]
    [ApiRequirePermission("fees_due")]
    public IActionResult FeesDue(int? yearId, int? classId, int? sectionId, int? feeTypeId, string? status, int page = 1)
    {
        yearId ??= lookup.GetCurrentYearId();
        var (data, total) = svc.GetFeesDue(yearId, classId, sectionId, feeTypeId, status, page, 20);
        return Ok(new {
            data, total, page, totalPages = Math.Max(1, (int)Math.Ceiling((double)total / 20)),
            years = lookup.GetYears(), classes = lookup.GetClasses(), sections = lookup.GetSections(),
            feeTypes = lookup.GetFeeTypes(), yearId, classId, sectionId, feeTypeId, status,
        });
    }

    [HttpPost("collect-fee")]
    [ApiRequirePermission("fees_due")]
    public IActionResult CollectFee([FromBody] CollectFeeReq req)
    {
        int uid = User.UserId() ?? 1;
        svc.CollectFee(req.StudentFeeId, req.PaidAmount, req.Discount, req.LateFine, req.PaymentMode, req.TransactionRef, req.Remarks, uid);
        return Ok(new { success = true });
    }

    [HttpPost("generate-monthly")]
    [ApiRequirePermission("fees_due")]
    public IActionResult GenerateMonthly(string month, int yearId)
    {
        int count = svc.GenerateMonthlyFees(month, yearId);
        return Ok(new { success = true, count });
    }
}
