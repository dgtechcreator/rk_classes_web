using Microsoft.AspNetCore.Hosting;
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
    FeeStructureService feeStructureSvc, IWebHostEnvironment env) : Controller
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

        // Extract additional charges from previous payments
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
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public IActionResult SavePay(int studentId, decimal payingNow, decimal additionalDiscount,
       decimal additionalCharges = 0, DateTime paymentDate = default, DateTime? dueDate = null,
       string paymentMode = "Cash", string? transactionRef = null, string? remarks = null)
    {
        try
        {
            // Get user ID from session with fallback
            int? sessionUserId = HttpContext.Session.GetUserId();
            if (!sessionUserId.HasValue)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Auth");
            }
            int uid = sessionUserId.Value;

            var student = studentSvc.GetById(studentId);
            if (student == null) return NotFound();

            // Set payment date to today if not provided
            if (paymentDate == default)
                paymentDate = DateTime.Today;

            var history = svc.GetStudentHistory(studentId);
            decimal existingDiscount = history.Sum(x => x.Discount);

            // Discount is only allowed once per student (first payment). On later
            // payments it's locked to 0 regardless of what the form posts.
            decimal discountToApply = existingDiscount > 0 ? 0 : additionalDiscount;

            // payingNow = the actual amount being received right now (e.g. 4000).
            // discountToApply = recorded purely for history/reporting (e.g. "2000 discount
            // given"). It must NOT be subtracted from payingNow again here or inside
            // FeesRepo.Save — NetAmount in the DB should equal payingNow as-is.

            // Note: additionalCharges is added to base fee for total fee calculation
            // Store it in remarks if present for tracking
            string finalRemarks = remarks ?? "";
            if (additionalCharges > 0)
                finalRemarks = (finalRemarks.Length > 0 ? finalRemarks + " | " : "") + $"Additional Charges: ₹{additionalCharges}";

            var payId = svc.Collect(studentId, null, payingNow, discountToApply, 0,
                paymentDate, paymentMode, transactionRef,
                student.AcademicYearId, null, finalRemarks, uid, dueDate);

            if (payId <= 0)
            {
                TempData["Error"] = "Failed to save payment. Please try again.";
                return RedirectToAction("Pay", new { id = studentId });
            }

            TempData["Success"] = "Fee collected successfully.";
            return RedirectToAction("Receipt", new { id = payId });
        }
        catch (Exception ex)
        {
            // Log the error details
            Console.WriteLine($"[SavePay Error] StudentId={studentId}, PayingNow={payingNow}, Error: {ex.Message}");

            TempData["Error"] = "Error saving payment: " + ex.Message;
            return RedirectToAction("Pay", new { id = studentId });
        }
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
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public IActionResult SavePayment(FeeCollectVM m)
    {
        try
        {
            int? sessionUserId = HttpContext.Session.GetUserId();
            if (!sessionUserId.HasValue)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Auth");
            }
            int uid = sessionUserId.Value;

            var payId = svc.Collect(m.StudentId, m.FeeTypeId, m.Amount, m.Discount, m.LateFine,
                DateTime.Today, m.PaymentMode, m.TransactionRef, m.AcademicYearId, m.Month, m.Remarks, uid);

            if (payId <= 0)
            {
                TempData["Error"] = "Failed to save payment. Please try again.";
                return RedirectToAction("Collect");
            }

            TempData["Success"] = "Fee collected successfully.";
            return RedirectToAction("Receipt", new { id = payId });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SavePayment Error] StudentId={m.StudentId}, Amount={m.Amount}, Error: {ex.Message}");
            TempData["Error"] = "Error saving payment: " + ex.Message;
            return RedirectToAction("Collect");
        }
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
        var paymentData = svc.GetPaymentById(id);
        if (paymentData == null) return NotFound();

        var (dueDate, remainingBalance) = ComputeDueInfo(paymentData);

        // If download parameter is present, generate the full PDF receipt directly
        if (download.HasValue && download == 1)
        {
            return GenerateReceiptPDF(paymentData, dueDate, remainingBalance);
        }

        // If share parameter is present, return JSON with PDF download URL
        if (download.HasValue && download == 2)
        {
            var downloadUrl = $"{Request.Scheme}://{Request.Host}/Fees/Receipt/{id}?download=1";
            return Json(new { success = true, pdfUrl = downloadUrl });
        }

        ViewBag.RemainingBalance = remainingBalance;
        ViewBag.DueDate = dueDate;

        return View(paymentData);
    }

    // Shared by the on-screen receipt and the PDF export so both show the same Due Date / Due Fees
    private (DateTime? DueDate, decimal RemainingBalance) ComputeDueInfo(FeePayment paymentData)
    {
        var student = studentSvc.GetById(paymentData.StudentId);
        if (student == null) return (null, 0);

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

        // Extract additional charges from all payment remarks
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

        decimal netFeeAmount = (totalFee + totalAdditionalCharges) - totalDiscount;
        decimal remainingBalance = Math.Max(0, netFeeAmount - totalPaid);

        DateTime? dueDate = null;
        var firstDue = structures.FirstOrDefault(x => x.DueDay > 0);
        if (firstDue != null)
            dueDate = new DateTime(paymentData.PaymentDate.Year, paymentData.PaymentDate.Month, 1)
                          .AddDays(firstDue.DueDay - 1);

        return (dueDate, remainingBalance);
    }

    // Mirrors the on-screen / "Print Landscape" receipt (Views/Fees/Receipt.cshtml) so the
    // PDF sent over WhatsApp looks the same as what staff see and print.
    private IActionResult GenerateReceiptPDF(FeePayment payment, DateTime? dueDate, decimal remainingBalance)
    {
        try
        {
            var fileName = $"Receipt_{payment.ReceiptNo}.pdf";
            var ms = new MemoryStream();

            var doc = new Document(PageSize.A5.Rotate(), 18, 18, 14, 14);
            PdfWriter.GetInstance(doc, ms);
            doc.Open();

            var titleFont  = new Font(Font.FontFamily.HELVETICA, 14, Font.BOLD);
            var boldSmall  = new Font(Font.FontFamily.HELVETICA, 9,  Font.BOLD);
            var labelFont  = new Font(Font.FontFamily.HELVETICA, 8,  Font.BOLD);
            var cellFont   = new Font(Font.FontFamily.HELVETICA, 8);
            var addrFont   = new Font(Font.FontFamily.HELVETICA, 7.5f);
            var termsFont  = new Font(Font.FontFamily.HELVETICA, 7);

            var outer = new PdfPTable(1) { WidthPercentage = 100 };
            var outerCell = new PdfPCell { Padding = 0 };

            // Top label row: "Kapil Sir's" | "SINCE : 2002"
            var topLabel = new PdfPTable(2) { WidthPercentage = 100 };
            var topLabelLeft = new PdfPCell(new Phrase("Kapil Sir's", boldSmall)) { Padding = 5 };
            topLabelLeft.BorderWidthBottom = 1;
            topLabel.AddCell(topLabelLeft);
            var topLabelRight = new PdfPCell(new Phrase("SINCE : 2002", boldSmall))
                { Padding = 5, HorizontalAlignment = Element.ALIGN_RIGHT };
            topLabelRight.BorderWidthBottom = 1;
            topLabel.AddCell(topLabelRight);
            outerCell.AddElement(topLabel);

            // Institute name
            var instName = new Paragraph("RAMJEET KALAVATI EDUCATIONAL INSTITUTE", titleFont)
                { Alignment = Element.ALIGN_CENTER, SpacingBefore = 5, SpacingAfter = 5 };
            outerCell.AddElement(instName);

            // Header row: logos + address | student info
            var header = new PdfPTable(2) { WidthPercentage = 100 };
            header.SetWidths(new float[] { 1.6f, 1f });

            var leftCell = new PdfPCell { Padding = 8 };
            leftCell.BorderWidthTop = 1;
            leftCell.BorderWidthBottom = 1;
            try
            {
                var logoPath = Path.Combine(env.WebRootPath, "images", "rkBw.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = iTextSharp.text.Image.GetInstance(logoPath);
                    logo.ScaleToFit(90, 55);
                    logo.SpacingAfter = 4;
                    leftCell.AddElement(logo);
                }
            }
            catch { /* logo is decorative — skip if it can't be loaded */ }
            leftCell.AddElement(new Paragraph(
                "Shop No. 2, Santosh Society, Krishna Nagar,\n" +
                "Near Eden School, Kajupada Pipe Line,\n" +
                "Sakinaka, Mumbai 400072.\n" +
                "E-Mail: rkclasseskapilsir2002@gmail.com\n" +
                "Website: www.myrkclasses.com\n" +
                "Mobile No: 9870375795 / 8108499214", addrFont));
            header.AddCell(leftCell);

            var rightCell = new PdfPCell { Padding = 8 };
            rightCell.BorderWidthTop = 1;
            rightCell.BorderWidthBottom = 1;
            rightCell.BorderWidthLeft = 1;
            rightCell.AddElement(new Paragraph((payment.StudentName ?? "—").ToUpper(), boldSmall));
            rightCell.AddElement(new Paragraph($"Address: {payment.StudentAddress ?? "-"}", cellFont));
            rightCell.AddElement(new Paragraph($"Contact No.: {payment.FatherPhone ?? payment.StudentPhone ?? "-"}", cellFont));
            rightCell.AddElement(new Paragraph($"E-Mail: {payment.StudentEmail ?? "-"}", cellFont));
            header.AddCell(rightCell);

            outerCell.AddElement(header);

            // Receipt details table (same fields as the on-screen receipt)
            var payYear = payment.PaymentDate.Month >= 4
                ? $"{payment.PaymentDate.Year}-{payment.PaymentDate.Year + 1}"
                : $"{payment.PaymentDate.Year - 1}-{payment.PaymentDate.Year}";
            var amtWords  = AmountInWords(payment.NetAmount) + " Only/-";
            var isCheque  = payment.PaymentMode == "Cheque" || payment.PaymentMode == "Demand Draft";
            var isOnline  = payment.PaymentMode == "Online Transfer" || payment.PaymentMode == "UPI";
            var chequeNo  = isCheque ? (payment.TransactionRef ?? "") : "";
            var onlineRef = isOnline ? (payment.TransactionRef ?? "") : "";

            PdfPCell Cell(string text, int colspan = 1) =>
                new PdfPCell(new Phrase(text, cellFont)) { Colspan = colspan, Padding = 5 };

            var details = new PdfPTable(3) { WidthPercentage = 100 };
            details.AddCell(new PdfPCell(new Phrase($"Fees Receipt ({payYear})", labelFont)) { Padding = 5 });
            details.AddCell(Cell($"Receipt Date: {payment.PaymentDate:dd-MM-yyyy}"));
            details.AddCell(Cell($"Receipt No.: {payment.ReceiptNo}"));

            details.AddCell(Cell($"Course:- {payment.ClassName ?? "—"}"));
            details.AddCell(Cell($"Stream / Medium:- {payment.SectionName ?? "—"}"));
            details.AddCell(Cell($"Batch:- {payment.BatchName ?? "—"}"));

            details.AddCell(Cell($"Amount received:- ₹{payment.NetAmount:N0}/-", 3));
            details.AddCell(Cell($"Amount received (in words):- {amtWords}", 3));

            details.AddCell(Cell($"Payment Mode:- {payment.PaymentMode}"));
            details.AddCell(Cell($"Cheque No.: {(string.IsNullOrWhiteSpace(chequeNo) ? "NA" : chequeNo)}", 2));

            details.AddCell(Cell($"Cheque Dated:- {(string.IsNullOrWhiteSpace(chequeNo) ? "NA" : "-")}"));
            details.AddCell(Cell($"Bank Name:- {(string.IsNullOrWhiteSpace(chequeNo) ? "NA" : "-")}", 2));

            details.AddCell(Cell($"IFSC Code:- {(string.IsNullOrWhiteSpace(chequeNo) ? "NA" : "-")}"));
            details.AddCell(Cell($"Online Tranx. No.:- {(string.IsNullOrWhiteSpace(onlineRef) ? "NA" : onlineRef)}", 2));

            details.AddCell(Cell($"Due Date:- {(dueDate.HasValue ? dueDate.Value.ToString("dd-MM-yyyy") : "Nil")}"));
            details.AddCell(Cell($"Due fees:- {(remainingBalance > 0 ? "₹" + remainingBalance.ToString("N0") : "Nil")}", 2));

            outerCell.AddElement(details);

            // Terms & conditions
            var terms = new Paragraph { SpacingBefore = 6 };
            terms.Add(new Chunk("Term and Conditions:\n", labelFont));
            terms.Add(new Chunk(
                "1) This receipt of fees is an acknowledgement of the payment made to\n" +
                "2) Present this receipt of fees whenever demanded.\n" +
                "3) Fees once paid is neither refundable nor transferable under any circumstances\n" +
                "4) This is a computer generated voucher, signature is not required", termsFont));
            var termsCell = new PdfPCell { Padding = 8 };
            termsCell.BorderWidthTop = 1;
            termsCell.AddElement(terms);
            var termsTable = new PdfPTable(1) { WidthPercentage = 100 };
            termsTable.AddCell(termsCell);
            outerCell.AddElement(termsTable);

            outer.AddCell(outerCell);
            doc.Add(outer);

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
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public IActionResult DeletePayment(int id, int? returnStudentId)
    {
        try
        {
            int? sessionUserId = HttpContext.Session.GetUserId();
            if (!sessionUserId.HasValue)
            {
                TempData["Error"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Auth");
            }
            int uid = sessionUserId.Value;

            svc.DeletePayment(id, uid);
            TempData["Success"] = "Payment deleted — balance has been reversed.";
            // If called from Pay page, go back to that student's Pay page
            if (returnStudentId.HasValue)
                return RedirectToAction("Pay", new { id = returnStudentId.Value });
            return RedirectToAction("Summary");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeletePayment Error] PaymentId={id}, Error: {ex.Message}");
            TempData["Error"] = "Error deleting payment: " + ex.Message;
            if (returnStudentId.HasValue)
                return RedirectToAction("Pay", new { id = returnStudentId.Value });
            return RedirectToAction("Summary");
        }
    }
}
