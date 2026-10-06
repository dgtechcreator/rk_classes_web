using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/students")]
[ApiRequireStaff]
public class StudentApiController(StudentService svc, LookupService lookup, FeeStructureService feeSvc) : ControllerBase
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
        if (isNew && id > 0) feeSvc.ApplyToStudent(id);
        return Ok(new { studentId = id, isNew });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("student", requireEdit: true)]
    public IActionResult Delete(int id)
    {
        svc.Delete(id);
        return Ok(new { success = true });
    }

    [HttpGet("by-class-batch")]
    public IActionResult GetByClassAndBatch(string className, string batchName)
        => Ok(svc.GetByClassAndBatch(className, batchName));

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
                fullName = s.FullName,
                admissionNo = s.AdmissionNo,
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
                        fullName = s.FullName,
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
