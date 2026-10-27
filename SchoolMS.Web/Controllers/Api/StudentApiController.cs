using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/students")]
[ApiRequireStaff]
public class StudentApiController(StudentService svc, LookupService lookup, FeeStructureService feeSvc, ProfilePhotoStore photos,
    AttendanceService attSvc, MarksService marksSvc, FeePositionService feePos) : ControllerBase
{
    [HttpGet]
    [ApiRequirePermission("student_view")]
    public IActionResult Index(int page = 1, string? search = null, int? classId = null,
        int? sectionId = null, int? batchId = null, string? status = "Active")
    {
        var (data, total) = svc.GetAll(1, 999999, search, classId, sectionId, batchId, null, status);
        return Ok(new { students = data, total });
    }

    [HttpGet("{id:int}")]
    [ApiRequirePermission("student_view")]
    public IActionResult GetById(int id)
    {
        var s = svc.GetById(id);
        if (s == null) return NotFound();
        var fees = feeSvc.GetStudentFees(id);
        return Ok(new { student = s, fees });
    }

    [HttpPost("save")]
    [ApiRequirePermission("student", requireEdit: true)]
    public IActionResult Save([FromBody] Student model)
    {
        int uid = User.UserId() ?? 1;
        bool isNew = model.StudentId == 0;
        var id = svc.Save(model, null, uid);
        if (id > 0) feeSvc.ApplyToStudent(id);   // also re-syncs fees when class/section changed
        return Ok(new { studentId = id, isNew });
    }

    public record AgreedDiscountReq(decimal Amount, string? Reason);

    /// <summary>
    /// Sets the discount agreed at admission (0 clears it). Separate from save on purpose: an older app build that
    /// edits a student never sends this, so it can never wipe an agreed discount by accident.
    /// </summary>
    [HttpPost("{id:int}/agreed-discount")]
    [ApiRequirePermission("fee_collection")]
    public IActionResult SetAgreedDiscount(int id, [FromBody] AgreedDiscountReq req)
    {
        if (svc.GetById(id) == null) return NotFound(new { error = "Student not found." });
        var amount = req.Amount;
        var reason = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
        if (amount < 0) return BadRequest(new { error = "Agreed discount cannot be negative." });
        if (amount > 0 && reason == null) return BadRequest(new { error = "Please enter the reason for the agreed discount." });
        var baseFee = feeSvc.GetAllForStudent(id).Sum(x => x.Amount);
        if (baseFee > 0 && amount > baseFee)
            return BadRequest(new { error = $"Agreed discount (₹{amount:N0}) is more than the fee (₹{baseFee:N0})." });
        svc.SetAgreedDiscount(id, amount, reason, User.UserId() ?? 1);
        return Ok(new { success = true, agreedDiscount = amount, agreedDiscountReason = amount > 0 ? reason : null });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("student", requireEdit: true)]
    public IActionResult Delete(int id)
    {
        svc.Delete(id);
        return Ok(new { success = true });
    }

    /// <summary>Upload / replace the student's photo (multipart field "photo"). Same file + DB column the web form uses.</summary>
    [HttpPost("{id:int}/photo")]
    [ApiRequirePermission("student", requireEdit: true)]
    public IActionResult UploadPhoto(int id, IFormFile? photo)
    {
        var s = svc.GetById(id);
        if (s == null) return NotFound(new { error = "Student not found." });
        var (path, err) = photos.Save(photo, "students");
        if (err != null) return BadRequest(new { error = err });
        svc.SetPhoto(id, path);
        photos.Delete(s.ProfilePicPath);
        return Ok(new { profilePicPath = path });
    }

    [HttpDelete("{id:int}/photo")]
    [ApiRequirePermission("student", requireEdit: true)]
    public IActionResult DeletePhoto(int id)
    {
        var s = svc.GetById(id);
        if (s == null) return NotFound(new { error = "Student not found." });
        svc.SetPhoto(id, null);
        photos.Delete(s.ProfilePicPath);
        return Ok(new { profilePicPath = (string?)null });
    }

    [HttpGet("by-class-batch")]
    public IActionResult GetByClassAndBatch(string className, string batchName)
        => Ok(svc.GetByClassAndBatch(className, batchName));

    /// <summary>
    /// One student's report (attendance, marks, fees) for staff - same JSON as the parent dashboard so the app draws both with one
    /// screen. Rights: the "Student 360 View" module (student_profile), so the admin decides who sees it (principal, head master...).
    /// </summary>
    [HttpGet("{id:int}/report")]
    [ApiRequirePermission("student_profile")]
    public IActionResult Report(int id)
    {
        var student = svc.GetById(id);
        if (student == null) return NotFound(new { error = "Student not found." });
        var fee = feePos.Calculate(student);
        return Ok(new {
            student,
            attendance = attSvc.GetStudentAttendanceSummary(id),
            attendanceDetail = attSvc.GetStudentAttendanceDetail(id),
            marks = marksSvc.GetStudentAllMarks(id),
            feeHistory = fee.History,
            totalPaid = fee.Paid,
            actualFee = fee.BaseFee,
            additionalCharges = fee.AdditionalCharges,
            discount = fee.Discount,
            netTotal = fee.NetTotal,
            balance = fee.Balance,
            dueDate = fee.InstallmentDate,
            installmentDate = fee.InstallmentDate,
            feeStructures = fee.Structures,
        });
    }

    [HttpGet("quick-search")]
    public IActionResult QuickSearch(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new List<object>());

        var yearId = lookup.GetCurrentYearId();
        var (students, _) = svc.GetAll(1, 50, q, null, null, null, yearId, "Active");

        var classes = lookup.GetClasses();
        var sections = lookup.GetSections();

        var results = students
            .Select(s => new {
                studentId = s.StudentId,
                classId = s.ClassId,
                sectionId = s.SectionId,
                fullName = s.DisplayName,
                admissionNo = s.AdmissionNo,
                profilePicPath = s.ProfilePicPath,
                className = classes.FirstOrDefault(c => c.ClassId == s.ClassId)?.ClassName ?? "",
                sectionName = sections.FirstOrDefault(sec => sec.SectionId == s.SectionId)?.SectionName ?? ""
            })
            .ToList();

        return Ok(results);
    }

    [HttpGet("class/{classId:int}")]
    public IActionResult GetClassStudents(int classId)
    {
        try
        {
            var yearId = lookup.GetCurrentYearId();
            var (students, _) = svc.GetAll(1, 999999, null, classId, null, null, yearId, "Active");

            var batches = lookup.GetBatches();
            var sections = lookup.GetSections();
            var classes = lookup.GetClasses();
            var className = classes.FirstOrDefault(c => c.ClassId == classId)?.ClassName ?? "";

            var results = new {
                className,
                students = students
                    .Select(s => new {
                        studentId = s.StudentId,
                        fullName = s.DisplayName,
                        admissionNo = s.AdmissionNo,
                        fatherPhone = s.FatherPhone ?? "",
                        motherPhone = s.MotherPhone ?? "",
                        batchName = batches.FirstOrDefault(b => b.BatchId == s.BatchId)?.BatchName ?? "",
                        sectionName = sections.FirstOrDefault(sec => sec.SectionId == s.SectionId)?.SectionName ?? ""
                    })
                    .ToList()
            };

            return Ok(results);
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message, className = "", students = new List<object>() });
        }
    }
}
