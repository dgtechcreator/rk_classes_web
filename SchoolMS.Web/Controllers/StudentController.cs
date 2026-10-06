using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class StudentController(StudentService svc, LookupService lookup, FeeStructureService feeSvc,
    AttendanceService attSvc, MarksService marksSvc, FeesService feesSvc, FeePositionService feePos) : Controller
{
    public IActionResult Index(int page=1, string? search=null, int? classId=null,
        int? sectionId=null, int? batchId=null, string? status="Active")
    {
        var (data, total) = svc.GetAll(1, 999999, search, classId, sectionId, batchId, null, status);
        return View(new StudentListVM {
            Students=data, Total=total, Page=1, Search=search,
            ClassFilter=classId, SectionFilter=sectionId, BatchFilter=batchId, StatusFilter=status,
            Classes=lookup.GetClasses(), Sections=lookup.GetSections(), Batches=lookup.GetBatches()
        });
    }

    public IActionResult Create()
    {
        var currentYearId = lookup.GetCurrentYearId();
        return View("Form", new StudentFormVM {
            Student=new Student { AcademicYearId=currentYearId },
            Years=lookup.GetYears(),
            Classes=lookup.GetClasses(), Sections=lookup.GetSections(), Batches=lookup.GetBatches()
        });
    }

    public IActionResult Edit(int id)
    {
        var s = svc.GetById(id);
        if (s == null) return NotFound();
        return View("Form", new StudentFormVM {
            Student=s, Years=lookup.GetYears(),
            Classes=lookup.GetClasses(), Sections=lookup.GetSections(), Batches=lookup.GetBatches()
        });
    }

    [HttpPost]
    public IActionResult Save(StudentFormVM model)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        string? picPath = null;
        if (model.ProfilePic != null && model.ProfilePic.Length > 0)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "students");
            Directory.CreateDirectory(dir);
            var fn = $"{Guid.NewGuid()}{Path.GetExtension(model.ProfilePic.FileName)}";
            using var fs = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            model.ProfilePic.CopyTo(fs);
            picPath = $"/images/students/{fn}";
        }
        bool isNew = model.Student.StudentId == 0;
        var id = svc.Save(model.Student, picPath, uid);
        if (isNew && id > 0) feeSvc.ApplyToStudent(id);
        TempData["Success"] = "Student saved successfully.";
        TempData["IsNewStudent"] = isNew;
        TempData["StudentName"] = model.Student.FullName;
        TempData["FatherPhone"] = model.Student.FatherPhone;
        TempData["MotherPhone"] = model.Student.MotherPhone;
        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        svc.Delete(id);
        TempData["Success"] = "Student marked as Inactive.";
        return RedirectToAction("Index");
    }

    public IActionResult Details(int id)
    {
        var s = svc.GetById(id);
        if (s == null) return NotFound();
        var fees = feeSvc.GetStudentFees(id);
        ViewBag.StudentFees = fees;

        // Fee position (same rule as the Fees > Pay page): base fee + additional charges (kept in payment
        // remarks) - discount - paid. Only for staff who may see fee data.
        if (HttpContext.Session.HasPerm("fee_collection"))
        {
            var pos = feePos.Calculate(s);
            ViewBag.FeeTotal = pos.NetTotal;        // fee + additional charges - discount
            ViewBag.FeeBase = pos.BaseFee;
            ViewBag.FeeExtra = pos.AdditionalCharges;
            ViewBag.FeeDiscount = pos.Discount;
            ViewBag.FeePaid = pos.Paid;
            ViewBag.FeeBalance = pos.Balance;
            ViewBag.FeeHistory = pos.History;
        }
        return View(s);
    }

    [HttpPost]
    public IActionResult SendWelcomeMessages(int studentId)
    {
        var student = svc.GetById(studentId);
        if (student == null) return Json(new { success = false, message = "Student not found" });

        string englishMsg = "*Dear Student,*\n\nWelcome to R K CLASSES. Wishing you a great learning experience and success in your studies.\n\n*Regards,*\n*R K CLASSES*\n*Kapil Sir*\n*M.Sc.,B.ed*\n*9870375795*\n*8108499214*";

        string hindiMsg = "*प्रिय विद्यार्थी,*\n\n*R K CLASSES* में आपका स्वागत है। आपके उज्ज्वल भविष्य और सफलता की कामना करते हैं।\n\n*सादर,*\n*R K CLASSES*\n*Kapil Sir*\n*M.Sc.,B.ed*\n*9870375795*\n*8108499214*";

        try
        {
            int messagesSent = 0;

            // Send BOTH messages to Father
            if (!string.IsNullOrEmpty(student.FatherPhone))
            {
                SendWhatsAppMessage(student.FatherPhone, englishMsg);
                SendWhatsAppMessage(student.FatherPhone, hindiMsg);
                messagesSent += 2;
            }

            // Send BOTH messages to Mother/Student
            if (!string.IsNullOrEmpty(student.Phone))
            {
                SendWhatsAppMessage(student.Phone, englishMsg);
                SendWhatsAppMessage(student.Phone, hindiMsg);
                messagesSent += 2;
            }

            if (messagesSent > 0)
            {
                return Json(new { success = true, message = $"✓ {messagesSent} messages sent successfully!" });
            }
            else
            {
                return Json(new { success = false, message = "No contact numbers available" });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error: " + ex.Message });
        }
    }

    private void SendWhatsAppMessage(string phone, string message)
    {
        try
        {
            string formattedPhone = phone.Replace("+", "").Replace(" ", "").Replace("-", "").Trim();

            // Using WhatsApp Web Green API or similar service
            // Configure these settings in appsettings.json
            string apiUrl = "https://api.green-api.com/waapi/SendMessage";
            string instanceId = "Your_Instance_ID"; // Configure in appsettings
            string token = "Your_API_Token"; // Configure in appsettings

            // For demo: Log the message instead (replace with actual API call)
            System.Diagnostics.Debug.WriteLine($"WhatsApp message to {formattedPhone}: {message}");

            // TODO: Integrate with WhatsApp Business API, Twilio, or Green API
            // Example for future integration:
            // using (var client = new HttpClient())
            // {
            //     var content = new StringContent(
            //         JsonConvert.SerializeObject(new { phoneNumber = formattedPhone, message = message }),
            //         Encoding.UTF8, "application/json");
            //     await client.PostAsync(apiUrl, content);
            // }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    public IActionResult AdmissionForm(int id)
    {
        var student = svc.GetById(id);
        if (student == null) return NotFound();
        return View(student);
    }

    public IActionResult ExportExcel(string? search=null, int? classId=null,
        int? sectionId=null, int? batchId=null, string? status=null)
    {
        var (data, _) = svc.GetAll(1, 100000, search, classId, sectionId, batchId, null, status);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Sr No,Full Name,Father Name,Mother Name,Contact No,Father Contact,Mother Contact,Class,Medium/Stream,Batch,Address");

        int sr = 0;
        foreach (var s in data)
        {
            sr++;
            sb.AppendLine(string.Join(",",
                sr,
                Csv(s.FullName),
                Csv(s.FatherName),
                Csv(s.MotherName),
                Csv(s.Phone),
                Csv(s.FatherPhone),
                Csv(s.MotherPhone),
                Csv(s.ClassName),
                Csv(s.SectionName),
                Csv(s.BatchName),
                Csv(s.Address)
            ));
        }

        var fileName = $"Students_{DateTime.Today:ddMMMyyy}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    // ── 360 Profile Excel Export ─────────────────────────────────
    public IActionResult ExportProfile360(int studentId, string? sections = null)
    {
        var student = svc.GetById(studentId);
        if (student == null) return NotFound();

        // Parse requested sections (default: all)
        var sec = (sections ?? "info,att,marks,fees")
                  .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(s => s.ToLower())
                  .ToHashSet();

        bool inclInfo  = sec.Contains("info");
        bool inclAtt   = sec.Contains("att");
        bool inclMarks = sec.Contains("marks");
        bool inclFees  = sec.Contains("fees");

        // Only fetch data that is needed
        var att   = inclAtt   ? attSvc.GetStudentAttendanceSummary(studentId)  : null;
        var attDet = inclAtt  ? attSvc.GetStudentAttendanceDetail(studentId)   : new();
        var marks = inclMarks ? marksSvc.GetStudentAllMarks(studentId)         : new();
        var fees  = inclFees  ? feesSvc.GetStudentHistory(studentId)           : new();

        var sb = new System.Text.StringBuilder();

        // ── Personal Info ──
        if (inclInfo)
        {
            sb.AppendLine("=== PERSONAL INFORMATION ===");
            sb.AppendLine("Field,Value");
            sb.AppendLine($"Full Name,{Csv(student.FullName)}");
            sb.AppendLine($"Admission No,{Csv(student.AdmissionNo)}");
            sb.AppendLine($"Roll No,{Csv(student.RollNo)}");
            sb.AppendLine($"Class,{Csv(student.ClassName)}");
            sb.AppendLine($"Medium/Stream,{Csv(student.SectionName)}");
            sb.AppendLine($"Batch,{Csv(student.BatchName)}");
            sb.AppendLine($"Academic Year,{Csv(student.YearName)}");
            sb.AppendLine($"Date of Birth,{student.DateOfBirth?.ToString("dd MMM yyyy")}");
            sb.AppendLine($"Gender,{Csv(student.Gender)}");
            sb.AppendLine($"Blood Group,{Csv(student.BloodGroup)}");
            sb.AppendLine($"Aadhaar No,{Csv(student.AadhaarNo)}");
            sb.AppendLine($"Admission Date,{student.AdmissionDate?.ToString("dd MMM yyyy")}");
            sb.AppendLine($"Status,{Csv(student.Status)}");
            sb.AppendLine($"Father Name,{Csv(student.FatherName)}");
            sb.AppendLine($"Father Phone,{Csv(student.FatherPhone)}");
            sb.AppendLine($"Father Occupation,{Csv(student.FatherOccupation)}");
            sb.AppendLine($"Mother Name,{Csv(student.MotherName)}");
            sb.AppendLine($"Mother Phone,{Csv(student.MotherPhone)}");
            sb.AppendLine($"Mother Occupation,{Csv(student.MotherOccupation)}");
            sb.AppendLine($"Student Phone,{Csv(student.Phone)}");
            sb.AppendLine($"Email,{Csv(student.Email)}");
            sb.AppendLine($"Address,{Csv(student.Address)}");
            sb.AppendLine($"City,{Csv(student.City)}");
            sb.AppendLine($"State,{Csv(student.State)}");
            sb.AppendLine($"Pincode,{Csv(student.Pincode)}");
            sb.AppendLine($"Previous School,{Csv(student.PreviousSchool)}");
            sb.AppendLine($"Previous %,{Csv(student.PreviousPercentage)}");
            sb.AppendLine();
        }

        // ── Attendance ──
        if (inclAtt && att != null)
        {
            sb.AppendLine("=== ATTENDANCE SUMMARY ===");
            sb.AppendLine("Present Days,Absent Days,Late Days,Total Days,Attendance %");
            sb.AppendLine($"{att.PresentDays},{att.AbsentDays},{att.LateDays},{att.TotalDays},{att.AttendancePct:0.1}%");
            sb.AppendLine();
            if (attDet.Count > 0)
            {
                sb.AppendLine("=== ATTENDANCE DETAIL ===");
                sb.AppendLine("Date,Status,Subject,Teacher,Remarks");
                foreach (var d in attDet)
                    sb.AppendLine(string.Join(",",
                        d.AttendanceDate.ToString("dd MMM yyyy"),
                        Csv(d.Status),
                        Csv(d.Subject),
                        Csv(d.SirName),
                        Csv(d.Remarks)));
                sb.AppendLine();
            }
        }

        // ── Marks ──
        if (inclMarks)
        {
            sb.AppendLine("=== TEST MARKS ===");
            sb.AppendLine("Exam Name,Test Date,Subject,Marks Obtained,Max Marks,Percentage,Grade,Result");
            foreach (var m in marks.OrderByDescending(x => x.TestDate).ThenBy(x => x.SubjectName))
            {
                bool isAb  = m.Grade == "AB";
                decimal pct = m.MaxMarks > 0 && m.MarksObtained.HasValue
                    ? Math.Round(m.MarksObtained.Value * 100m / m.MaxMarks, 1) : 0;
                string result = isAb ? "ABSENT" : pct >= 35 ? "PASS" : "FAIL";
                sb.AppendLine(string.Join(",",
                    Csv(m.ExamName),
                    m.TestDate?.ToString("dd MMM yyyy") ?? "",
                    Csv(m.SubjectName),
                    isAb ? "AB" : m.MarksObtained?.ToString("0") ?? "",
                    m.MaxMarks.ToString(),
                    isAb ? "AB" : pct.ToString("0.00"),
                    Csv(m.Grade),
                    result));
            }
            sb.AppendLine();
        }

        // ── Fees ──
        if (inclFees)
        {
            sb.AppendLine("=== FEE PAYMENTS ===");
            sb.AppendLine("Receipt No,Date,Fee Type,Payment Mode,Amount,Discount,Net Paid");
            foreach (var f in fees.OrderByDescending(x => x.PaymentDate))
                sb.AppendLine(string.Join(",",
                    Csv(f.ReceiptNo),
                    f.PaymentDate.ToString("dd MMM yyyy"),
                    Csv(f.FeeTypeName),
                    Csv(f.PaymentMode),
                    f.Amount.ToString("0"),
                    f.Discount.ToString("0"),
                    f.NetAmount.ToString("0")));
            sb.AppendLine();
            sb.AppendLine($"Total Fees Paid,,,,,, {fees.Sum(f => f.NetAmount):0}");
        }

        var fileName = $"Profile360_{student.FullName.Replace(" ","_")}_{DateTime.Today:ddMMMyyyy}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    // ── 360 Profile ──────────────────────────────────────────────
    public IActionResult Profile360(int? classId, int? sectionId, int? studentId)
    {
        var classes  = lookup.GetClasses();
        var sections = lookup.GetSections();

        List<Student> studentList = new();
        if (classId.HasValue)
            studentList = svc.GetAll(1, 500, null, classId, sectionId, null, null, "Active").data;

        Student?          student = null;
        AttendanceReport? att     = null;
        List<TestMark>    marks   = new();
        List<FeePayment>  fees    = new();

        List<SchoolMS.Domain.StudentAttendanceDetail> attDetail = new();
        if (studentId.HasValue)
        {
            student   = svc.GetById(studentId.Value);
            att       = attSvc.GetStudentAttendanceSummary(studentId.Value);
            attDetail = attSvc.GetStudentAttendanceDetail(studentId.Value);
            marks     = marksSvc.GetStudentAllMarks(studentId.Value);
            fees      = feesSvc.GetStudentHistory(studentId.Value);
        }

        // Get total actual fees for the student
        var feeStructures = student != null && student.ClassId.HasValue
            ? feeSvc.GetAll(student.AcademicYearId, student.ClassId, student.SectionId)
            : new List<SchoolMS.Domain.FeeStructure>();
        if (!feeStructures.Any() && student != null && student.ClassId.HasValue)
            feeStructures = feeSvc.GetAll(null, student.ClassId, student.SectionId);
        if (!feeStructures.Any() && student != null && student.ClassId.HasValue)
            feeStructures = feeSvc.GetAll(null, student.ClassId, null);

        decimal totalActualFees = feeStructures.Sum(f => f.Amount);
        decimal totalPaidFees = fees.Sum(f => f.NetAmount);
        decimal totalDiscount = fees.Sum(f => f.Discount);

        // Extract additional charges from remarks
        decimal totalAdditionalCharges = 0;
        foreach (var payment in fees)
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

        decimal totalFeeWithAdditional = totalActualFees + totalAdditionalCharges;
        decimal feesAfterDiscount = totalFeeWithAdditional - totalDiscount;
        decimal balanceDue = Math.Max(0, totalFeeWithAdditional - totalDiscount - totalPaidFees);

        ViewBag.Classes     = classes;
        ViewBag.Sections    = sections;
        ViewBag.StudentList = studentList;
        ViewBag.ClassId     = classId;
        ViewBag.SectionId   = sectionId;
        ViewBag.StudentId   = studentId;
        ViewBag.Attendance  = att;
        ViewBag.AttDetail   = attDetail;
        ViewBag.Marks       = marks;
        ViewBag.Fees        = fees;
        ViewBag.TotalActualFees = totalActualFees;
        ViewBag.TotalPaidFees = totalPaidFees;
        ViewBag.TotalDiscount = totalDiscount;
        ViewBag.FeesAfterDiscount = feesAfterDiscount;
        ViewBag.BalanceDue = balanceDue;
        return View(student);
    }

    [HttpGet]
    public IActionResult QuickSearch(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Json(new List<object>());

        var yearId = lookup.GetCurrentYearId();
        var (students, _) = svc.GetAll(1, 50, q, null, null, null, yearId, "Active");

        var classes = lookup.GetClasses();
        var sections = lookup.GetSections();

        var results = students
            .Select(s => new {
                studentId = s.StudentId,
                classId = s.ClassId,
                sectionId = s.SectionId,
                fullName = s.FullName,
                admissionNo = s.AdmissionNo,
                className = classes.FirstOrDefault(c => c.ClassId == s.ClassId)?.ClassName ?? "",
                sectionName = sections.FirstOrDefault(sec => sec.SectionId == s.SectionId)?.SectionName ?? ""
            })
            .ToList();

        return Json(results);
    }

    [HttpGet]
    public IActionResult GetClassStudents(int classId)
    {
        try
        {
            var yearId = lookup.GetCurrentYearId();
            var (students, _) = svc.GetAll(1, 999999, null, classId, null, null, yearId, null);

            var batches = lookup.GetBatches();
            var sections = lookup.GetSections();
            var classes = lookup.GetClasses();
            var className = classes.FirstOrDefault(c => c.ClassId == classId)?.ClassName ?? "";

            var results = new {
                className = className,
                students = students
                    .Select(s => new {
                        studentId = s.StudentId,
                        fullName = s.FullName,
                        admissionNo = s.AdmissionNo,
                        fatherPhone = s.FatherPhone ?? "",
                        motherPhone = s.MotherPhone ?? "",
                        batchName = batches.FirstOrDefault(b => b.BatchId == s.BatchId)?.BatchName ?? "",
                        sectionName = sections.FirstOrDefault(sec => sec.SectionId == s.SectionId)?.SectionName ?? ""
                    })
                    .ToList()
            };

            return Json(results);
        }
        catch (Exception ex)
        {
            return Json(new { error = ex.Message, className = "", students = new List<object>() });
        }
    }

    static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
