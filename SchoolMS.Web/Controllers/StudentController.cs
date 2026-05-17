using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class StudentController(StudentService svc, LookupService lookup, FeeStructureService feeSvc) : Controller
{
    public IActionResult Index(int page=1, string? search=null, int? classId=null,
        int? sectionId=null, int? batchId=null, string? status=null)
    {
        var (data, total) = svc.GetAll(page, 15, search, classId, sectionId, batchId, null, status);
        return View(new StudentListVM {
            Students=data, Total=total, Page=page, Search=search,
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
        return RedirectToAction("Details", new { id });
    }

    public IActionResult Details(int id)
    {
        var s = svc.GetById(id);
        if (s == null) return NotFound();
        var fees = feeSvc.GetStudentFees(id);
        ViewBag.StudentFees = fees;
        return View(s);
    }

    public IActionResult ExportExcel(string? search=null, int? classId=null,
        int? sectionId=null, int? batchId=null, string? status=null)
    {
        var (data, _) = svc.GetAll(1, 100000, search, classId, sectionId, batchId, null, status);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Sr No,Admission No,Full Name,Father Name,Mother Name,Contact No,Father Contact,Mother Contact,Email,Class,Medium/Stream,Batch,Roll No,Blood Group,Date of Birth,Address,Status");

        int sr = 0;
        foreach (var s in data)
        {
            sr++;
            sb.AppendLine(string.Join(",",
                sr,
                Csv(s.AdmissionNo),
                Csv(s.FullName),
                Csv(s.FatherName),
                Csv(s.MotherName),
                Csv(s.Phone),
                Csv(s.FatherPhone),
                Csv(s.MotherPhone),
                Csv(s.Email),
                Csv(s.ClassName),
                Csv(s.SectionName),
                Csv(s.BatchName),
                Csv(s.RollNo),
                Csv(s.BloodGroup),
                s.DateOfBirth.HasValue ? s.DateOfBirth.Value.ToString("dd-MM-yyyy") : "",
                Csv(s.Address),
                Csv(s.Status)
            ));
        }

        var fileName = $"Students_{DateTime.Today:ddMMMyyy}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    static string Csv(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
