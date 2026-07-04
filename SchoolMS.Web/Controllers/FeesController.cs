using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace SchoolMS.Web.Controllers;

public class FeesController(FeesService svc, LookupService lookup,
    ClassFeeSetupService feeSetupSvc, StudentService studentSvc,
    FeeStructureService feeStructureSvc) : Controller
{
    // ── Page 1: Student search list ──────────────────────────────
    [RequireLogin]
    public IActionResult Index(int? yearId, int? sectionId, int? classId, bool search = false)
    {
        yearId ??= lookup.GetCurrentYearId();
        var years = lookup.GetYears();
        var sections = lookup.GetSections();
        var classes = lookup.GetClasses();

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
            Students = students,
            Years = years,
            Classes = classes,
            Sections = sections,
            YearFilter = yearId,
            ClassFilter = classId,
            SectionFilter = sectionId,
            Searched = search,
            FeeStructures = feeStructures
        });
    }

    // ── Page 2: Pay form for a specific student ──────────────────
    [RequireLogin]
    public IActionResult Pay(int id, int? paymentId)   // 'id' matches the {id} route segment in /Fees/Pay/8
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

        var vm = new FeePayVM
        {
            Student = student,
            ActualFee = actualFee,
            TotalPaid = totalPaid,
            Balance = Math.Max(0, balance),
            DueDate = dueDate,
            PaymentDate = DateTime.Today,
            FeeStructures = structures,
            PaymentHistory = history,
            ExistingDiscount = totalDiscount,
            EditPaymentId = null
        };

        // If paymentId provided, load existing payment for editing
        if (paymentId.HasValue)
        {
            var existingPayment = svc.GetPaymentById(paymentId.Value);
            if (existingPayment != null)
            {
                vm.PaymentDate = existingPayment.PaymentDate;
                vm.PayingNow = existingPayment.NetAmount;
                vm.AdditionalDiscount = existingPayment.Discount;
                vm.PaymentMode = existingPayment.PaymentMode;
                vm.TransactionRef = existingPayment.TransactionRef;
                vm.Remarks = existingPayment.Remarks;
                vm.EditPaymentId = paymentId.Value;
            }
        }

        return View(vm);
    }

    // ── Save payment ─────────────────────────────────────────────
    // Flow:
    //  - "payingNow" is whatever the user finally types in the box (after they've
    //    optionally used the discount field+Apply button to shrink the payable amount,
    //    and then possibly edited it further themselves).
    //  - Whatever lands in "payingNow" is exactly what should be recorded as NetAmount
    //    on the receipt — discount is recorded for history/reporting only and must NOT
    //    be subtracted again on the way into the database.
    [HttpPost]
    [RequireLogin]
    public IActionResult SavePay(int studentId, decimal payingNow, decimal additionalDiscount,
       DateTime paymentDate, DateTime? dueDate, string paymentMode = "Cash",
       string? transactionRef = null, string? remarks = null)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        var student = studentSvc.GetById(studentId);
        if (student == null) return NotFound();

        var history = svc.GetStudentHistory(studentId);
        decimal existingDiscount = history.Sum(x => x.Discount);

        // Discount is only allowed once per student (first payment). On later
        // payments it's locked to 0 regardless of what the form posts.
        decimal discountToApply = existingDiscount > 0 ? 0 : additionalDiscount;

        // payingNow = the actual amount being received right now (e.g. 4000).
        // discountToApply = recorded purely for history/reporting (e.g. "2000 discount
        // given"). It must NOT be subtracted from payingNow again here or inside
        // FeesRepo.Save — NetAmount in the DB should equal payingNow as-is.
        var payId = svc.Collect(studentId, null, payingNow, discountToApply, 0,
            paymentDate, paymentMode, transactionRef,
            student.AcademicYearId, null, remarks, uid, dueDate);

        TempData["Success"] = "Fee collected successfully.";
        return RedirectToAction("Receipt", new { id = payId });
    }

    // ── Legacy Collect (keep for backward compatibility) ─────────
    [RequireLogin]
    public IActionResult Collect()
    {
        var currentYearId = lookup.GetCurrentYearId();
        return View(new FeeCollectVM
        {
            Students = lookup.GetStudentDropdown(),
            FeeTypes = lookup.GetFeeTypes(),
            Years = lookup.GetYears(),
            Classes = lookup.GetClasses(),
            Batches = lookup.GetBatches(),
            Sections = lookup.GetSections(),
            AcademicYearId = currentYearId
        });
    }

    [RequireLogin]
    public IActionResult GetFeeAmount(int batchId, int classId, int sectionId)
    {
        var amount = feeSetupSvc.GetAmount(batchId, classId, sectionId);
        return Json(new { found = amount.HasValue, amount = amount ?? 0 });
    }

    [HttpPost]
    [RequireLogin]
    public IActionResult SavePayment(FeeCollectVM m)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        var payId = svc.Collect(m.StudentId, m.FeeTypeId, m.Amount, m.Discount, m.LateFine,
            DateTime.Today, m.PaymentMode, m.TransactionRef, m.AcademicYearId, m.Month, m.Remarks, uid);
        TempData["Success"] = "Fee collected successfully.";
        return RedirectToAction("Receipt", new { id = payId });
    }

    // ── Summary page ─────────────────────────────────────────────
    [RequireLogin]
    public IActionResult Summary(int page = 1, string? search = null, string? month = null, int? feeType = null)
    {
        var (data, total) = svc.GetAll(page, 15, search, month, feeType);
        var (allData, _) = svc.GetAll(1, 10000, search, month, feeType); // Get ALL matching records (not paginated)
        decimal grandTotal = allData.Sum(p => p.NetAmount);

        return View("_Summary", new FeesListVM
        {
            Payments = data,
            Total = total,
            GrandTotalAmount = grandTotal,
            Page = page,
            Search = search,
            MonthFilter = month,
            FeeTypeFilter = feeType,
            FeeTypes = lookup.GetFeeTypes()
        });
    }

    // ── Summary JSON for Dashboard popup ─────────────────────────
    [HttpGet]
    [RequireLogin]
    public IActionResult SummaryJson(int? monthNum = null)
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

    [RequireAdminOrParent]
    public IActionResult Receipt(int id, int? download = null)
    {
        // If download parameter is present, generate PDF directly
        if (download.HasValue && download == 1)
        {
            var paymentForPDF = svc.GetPaymentById(id);
            if (paymentForPDF == null) return NotFound();
            return GenerateReceiptPDF(paymentForPDF);
        }

        var paymentData = svc.GetPaymentById(id);
        if (paymentData == null) return NotFound();

        var student = studentSvc.GetById(paymentData.StudentId);
        if (student != null)
        {
            var structures = feeStructureSvc.GetAllForStudent(paymentData.StudentId);
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
            var history = svc.GetStudentHistory(paymentData.StudentId);
            decimal totalPaid = history.Sum(x => x.NetAmount);
            decimal totalDiscount = history.Sum(x => x.Discount);
            decimal netFeeAmount = totalFee - totalDiscount;
            decimal remainingBalance = netFeeAmount - totalPaid;

            ViewBag.TotalFee = totalFee;
            ViewBag.RemainingBalance = Math.Max(0, remainingBalance);

            var firstDue = structures.FirstOrDefault(x => x.DueDay > 0);
            if (firstDue != null)
                ViewBag.DueDate = new DateTime(paymentData.PaymentDate.Year,
                                               paymentData.PaymentDate.Month, 1)
                                      .AddDays(firstDue.DueDay - 1);
            else
                ViewBag.DueDate = null;
        }

        return View(paymentData);
    }

    private IActionResult GenerateReceiptPDF(FeePayment payment)
    {
        try
        {
            var fileName = $"Receipt_{payment.ReceiptNo}.pdf";
            var ms = new MemoryStream();

            var doc = new Document(PageSize.A5);
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            // Header - School Details
            var headerFont = new Font(Font.FontFamily.HELVETICA, 12, Font.BOLD);
            var titleFont = new Font(Font.FontFamily.HELVETICA, 11, Font.BOLD);
            var normalFont = new Font(Font.FontFamily.HELVETICA, 10);
            var smallFont = new Font(Font.FontFamily.HELVETICA, 9);

            var schoolName = new Paragraph("Ramjeet Kalavati Educational Institute", headerFont);
            schoolName.Alignment = Element.ALIGN_CENTER;
            doc.Add(schoolName);

            var schoolTag = new Paragraph("RK CLASSES (Since 2002)", titleFont);
            schoolTag.Alignment = Element.ALIGN_CENTER;
            doc.Add(schoolTag);

            var schoolAddr = new Paragraph("Sakinaka, Mumbai - 400072", smallFont);
            schoolAddr.Alignment = Element.ALIGN_CENTER;
            doc.Add(schoolAddr);

            doc.Add(new Paragraph(" "));

            // Receipt Details Table
            var table = new PdfPTable(3);
            table.WidthPercentage = 100;

            table.AddCell(new PdfPCell(new Phrase("Fees Receipt (2026-2027)", titleFont)));
            table.AddCell(new PdfPCell(new Phrase($"Receipt Date: {payment.PaymentDate:dd-MM-yyyy}", normalFont)));
            table.AddCell(new PdfPCell(new Phrase($"Receipt No.: {payment.ReceiptNo}", normalFont)));

            table.AddCell(new PdfPCell(new Phrase($"Course: {payment.ClassName}", normalFont)));
            table.AddCell(new PdfPCell(new Phrase($"Stream: {payment.SectionName}", normalFont)));
            table.AddCell(new PdfPCell(new Phrase($"Batch: {payment.BatchName}", normalFont)));

            var amountCell = new PdfPCell(new Phrase($"Amount Received: ₹{payment.NetAmount:N0}/-", normalFont));
            amountCell.Colspan = 3;
            table.AddCell(amountCell);

            var amountWordsCell = new PdfPCell(new Phrase($"Amount (in words): {AmountInWords(payment.NetAmount)} Only/-", smallFont));
            amountWordsCell.Colspan = 3;
            table.AddCell(amountWordsCell);

            table.AddCell(new PdfPCell(new Phrase($"Payment Mode: {payment.PaymentMode}", normalFont)));
            table.AddCell(new PdfPCell(new Phrase("Cheque No.: NA", normalFont)));
            table.AddCell(new PdfPCell(new Phrase("", normalFont)));

            doc.Add(table);
            doc.Add(new Paragraph(" "));

            // Student Info
            var studentInfo = new Paragraph($"Student: {payment.StudentName}\nAdmission No: {payment.AdmissionNo}", normalFont);
            doc.Add(studentInfo);

            doc.Add(new Paragraph(" "));

            // Terms
            var termsFont = new Font(Font.FontFamily.HELVETICA, 8);
            var terms = new Paragraph("Terms & Conditions: This receipt is an acknowledgement of the payment made. This is a computer generated receipt, signature is not required.", termsFont);
            doc.Add(terms);

            doc.Close();

            byte[] pdfBytes = ms.ToArray();
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error: {ex.Message}");
        }
    }

    private static string AmountInWords(decimal number)
    {
        if (number == 0) return "Zero";
        string[] ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
            "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
        long n = (long)number;
        string w = "";
        if (n >= 10000000) { w += AmountInWords(n / 10000000) + " Crore "; n %= 10000000; }
        if (n >= 100000) { w += AmountInWords(n / 100000) + " Lakh "; n %= 100000; }
        if (n >= 1000) { w += AmountInWords(n / 1000) + " Thousand "; n %= 1000; }
        if (n >= 100) { w += ones[n / 100] + " Hundred "; n %= 100; }
        if (n >= 20) { w += tens[n / 10] + " "; n %= 10; }
        if (n > 0) { w += ones[n] + " "; }
        return w.Trim();
    }

    [HttpPost]
    [RequireLogin]
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
