using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class FeesController(FeesService svc, LookupService lookup,
    ClassFeeSetupService feeSetupSvc, StudentService studentSvc,
    FeeStructureService feeStructureSvc) : Controller
{
    // ── Page 1: Student search list ──────────────────────────────
    public IActionResult Index(int? yearId, int? sectionId, int? classId, bool search = false)
    {
        yearId ??= lookup.GetCurrentYearId();
        var years    = lookup.GetYears();
        var sections = lookup.GetSections();
        var classes  = lookup.GetClasses();

        var students = new List<SchoolMS.Domain.Student>();
        if (search)
        {
            var (data, _) = studentSvc.GetAll(1, 500, null, classId, sectionId, null, yearId, "Active");
            students = data;
        }

        // Load fee structures for the selected class so the list can show fee amounts
        var feeStructures = classId.HasValue
            ? feeStructureSvc.GetAll(yearId, classId, sectionId)
            : new List<SchoolMS.Domain.FeeStructure>();
        // Fallback: if section given but nothing found, try class-only
        if (!feeStructures.Any() && classId.HasValue && sectionId.HasValue)
            feeStructures = feeStructureSvc.GetAll(yearId, classId, null);
        // Fallback: try any year with this class+section
        if (!feeStructures.Any() && classId.HasValue)
            feeStructures = feeStructureSvc.GetAll(null, classId, sectionId);
        // Final fallback: any year, any section, just class
        if (!feeStructures.Any() && classId.HasValue)
            feeStructures = feeStructureSvc.GetAll(null, classId, null);

        // Get student phone numbers for all students
        var studentPhones = new Dictionary<int, (string father, string mother)>();
        foreach (var student in students)
        {
            studentPhones[student.StudentId] = (student.FatherPhone ?? "", student.MotherPhone ?? "");
        }
        ViewBag.StudentPhones = studentPhones;

        return View(new FeesStudentListVM
        {
            Students      = students,
            Years         = years,
            Classes       = classes,
            Sections      = sections,
            YearFilter    = yearId,
            ClassFilter   = classId,
            SectionFilter = sectionId,
            Searched      = search,
            FeeStructures = feeStructures
        });
    }

    // ── Page 2: Pay form for a specific student ──────────────────
    public IActionResult Pay(int id)   // 'id' matches the {id} route segment in /Fees/Pay/8
    {
        var student = studentSvc.GetById(id);
        if (student == null) return NotFound();

        // Get fee structure for this student using smart multi-level lookup
        // (handles year/section ID mismatches between Students and FeeStructure tables)
        decimal actualFee = 0;
        DateTime? dueDate = null;
        var structures = feeStructureSvc.GetAllForStudent(id);

        // Final safety net: old-style ID fallbacks in case SP isn't deployed yet
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
                dueDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                              .AddDays(firstDue.DueDay - 1);
        }

        // Total paid and discounts so far
        var history = svc.GetStudentHistory(id);
        var totalPaid = history.Sum(x => x.NetAmount);
        var totalDiscount = history.Sum(x => x.Discount);
        var netActualFee = actualFee - totalDiscount;
        var balance = netActualFee - totalPaid;

        return View(new FeePayVM
        {
            Student        = student,
            ActualFee      = actualFee,
            TotalPaid      = totalPaid,
            Balance        = Math.Max(0, balance),
            DueDate        = dueDate,
            PaymentDate    = DateTime.Today,
            FeeStructures  = structures,
            PaymentHistory = history,
            ExistingDiscount = totalDiscount
        });
    }

    // ── Save payment ─────────────────────────────────────────────
    [HttpPost]
    public IActionResult SavePay(int studentId, decimal payingNow, decimal additionalDiscount,
       DateTime paymentDate, DateTime? dueDate, string paymentMode = "Cash",
       string? transactionRef = null, string? remarks = null)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        var student = studentSvc.GetById(studentId);
        if (student == null) return NotFound();

        var history = svc.GetStudentHistory(studentId);
        decimal existingDiscount = history.Sum(x => x.Discount);
        // Sirf pehli baar discount lagao
        decimal discountToApply = existingDiscount > 0 ? 0 : additionalDiscount;

        // ✅ payingNow = jo actually pay kiya (43000 ya partial)
        // ✅ discountToApply = sirf record ke liye, NetAmount affect nahi karega
        var payId = svc.Collect(studentId, null, payingNow, discountToApply, 0,
            paymentDate, paymentMode, transactionRef,
            student.AcademicYearId, null, remarks, uid, dueDate);

        TempData["Success"] = "Fee collected successfully.";
        return RedirectToAction("Receipt", new { id = payId });
    }

    // ── Legacy Collect (keep for backward compatibility) ─────────
    public IActionResult Collect()
    {
        var currentYearId = lookup.GetCurrentYearId();
        return View(new FeeCollectVM {
            Students  = lookup.GetStudentDropdown(),
            FeeTypes  = lookup.GetFeeTypes(),
            Years     = lookup.GetYears(),
            Classes   = lookup.GetClasses(),
            Batches   = lookup.GetBatches(),
            Sections  = lookup.GetSections(),
            AcademicYearId = currentYearId
        });
    }

    public IActionResult GetFeeAmount(int batchId, int classId, int sectionId)
    {
        var amount = feeSetupSvc.GetAmount(batchId, classId, sectionId);
        return Json(new { found = amount.HasValue, amount = amount ?? 0 });
    }

    [HttpPost]
    public IActionResult SavePayment(FeeCollectVM m)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        var payId = svc.Collect(m.StudentId, m.FeeTypeId, m.Amount, m.Discount, m.LateFine,
            DateTime.Today, m.PaymentMode, m.TransactionRef, m.AcademicYearId, m.Month, m.Remarks, uid);
        TempData["Success"] = "Fee collected successfully.";
        return RedirectToAction("Receipt", new { id = payId });
    }

    // ── Summary page ─────────────────────────────────────────────
    public IActionResult Summary(int page=1, string? search=null, string? month=null, int? feeType=null)
    {
        var (data, total) = svc.GetAll(page, 15, search, month, feeType);
        return View("_Summary", new FeesListVM {
            Payments=data, Total=total, Page=page, Search=search,
            MonthFilter=month, FeeTypeFilter=feeType, FeeTypes=lookup.GetFeeTypes()
        });
    }

    // ── Summary JSON for Dashboard popup ─────────────────────────
    [HttpGet]
    public IActionResult SummaryJson(int? monthNum=null)
    {
        var (data, _) = svc.GetAll(1, 100, null, null, null);

        // Filter by month from PaymentDate if monthNum provided
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
        return Json(result);
    }

    public IActionResult Receipt(int id)
    {
        var payment = svc.GetPaymentById(id);
        if (payment == null) return NotFound();

        var student = studentSvc.GetById(payment.StudentId);
        if (student != null)
        {
            var structures = feeStructureSvc.GetAllForStudent(payment.StudentId);
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

            decimal totalFee = structures.Sum(x => x.Amount);
            var history = svc.GetStudentHistory(payment.StudentId);
            decimal totalPaid = history.Sum(x => x.NetAmount);
            decimal totalDiscount = history.Sum(x => x.Discount);
            decimal netFeeAmount = totalFee - totalDiscount;
            decimal remainingBalance = netFeeAmount - totalPaid;

            ViewBag.TotalFee = totalFee;
            ViewBag.RemainingBalance = Math.Max(0, remainingBalance);

            // ✅ TempData ki jagah FeeStructure se due date calculate karo
            var firstDue = structures.FirstOrDefault(x => x.DueDay > 0);
            if (firstDue != null)
                ViewBag.DueDate = new DateTime(payment.PaymentDate.Year,
                                               payment.PaymentDate.Month, 1)
                                      .AddDays(firstDue.DueDay - 1);
            else
                ViewBag.DueDate = null;
        }

        return View(payment);
    }

    [HttpPost]
    public IActionResult DeletePayment(int id, int? returnStudentId)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        svc.DeletePayment(id, uid);
        TempData["Success"] = "Payment deleted — balance has been reversed.";
        // If called from Pay page, go back to that student's Pay page
        if (returnStudentId.HasValue)
            return RedirectToAction("Pay", new { id = returnStudentId.Value });
        return RedirectToAction("Summary");
    }
}
