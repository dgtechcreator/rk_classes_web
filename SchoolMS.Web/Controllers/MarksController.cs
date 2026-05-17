using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class MarksController(MarksService svc, LookupService lookup) : Controller
{
    public IActionResult Index(int? yearId, int? classId, int? sectionId, int? batchId,
        int? subjectId, string? examName, DateTime? testDate, int maxMarks = 30)
    {
        yearId   ??= lookup.GetCurrentYearId();
        testDate ??= DateTime.Today;

        var years    = lookup.GetYears();
        var classes  = lookup.GetClasses();
        var sections = lookup.GetSections();
        var batches  = lookup.GetBatches();
        var subjects = classId.HasValue ? lookup.GetSubjects(classId.Value) : new List<Subject>();
        var examList = svc.GetExamList(yearId, classId);

        if (subjectId == null && subjects.Count == 1) subjectId = subjects[0].SubjectId;

        var students = new List<StudentMarkRow>();
        if (classId.HasValue && subjectId.HasValue && !string.IsNullOrWhiteSpace(examName))
            students = svc.GetStudentsWithMarks(classId, sectionId, batchId, subjectId.Value, examName, testDate, yearId);
        else if (classId.HasValue)
            students = svc.GetStudentsForEntry(classId, sectionId, batchId, yearId);

        ViewBag.Years     = years;
        ViewBag.Classes   = classes;
        ViewBag.Sections  = sections;
        ViewBag.Batches   = batches;
        ViewBag.Subjects  = subjects;
        ViewBag.ExamList  = examList;
        ViewBag.YearId    = yearId;
        ViewBag.ClassId   = classId;
        ViewBag.SectionId = sectionId;
        ViewBag.BatchId   = batchId;
        ViewBag.SubjectId = subjectId;
        ViewBag.ExamName  = examName ?? "";
        ViewBag.TestDate  = testDate.Value.ToString("yyyy-MM-dd");
        ViewBag.MaxMarks  = maxMarks;
        return View(students);
    }

    [HttpPost]
    public IActionResult Save([FromBody] MarksSaveReq req)
    {
        try
        {
            int uid = HttpContext.Session.GetUserId() ?? 1;
            int yr  = req.YearId > 0 ? req.YearId : (lookup.GetCurrentYearId() ?? 1);
            DateTime? testDate = req.TestDate.HasValue ? req.TestDate.Value.Date : (DateTime?)null;
            foreach (var e in req.Entries)
                svc.SaveMarkDirect(e.StudentId, e.SubjectId, req.ExamName, yr, req.ClassId,
                    testDate, e.IsAbsent ? null : e.Marks, req.MaxMarks, e.IsAbsent, uid);
            return Json(new { success = true, message = $"Marks saved for {req.Entries.Count} students." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    public IActionResult Report(int? yearId, int? classId, int? sectionId, int? batchId,
        int? studentId, string? examName, DateTime? testDate)
    {
        yearId ??= lookup.GetCurrentYearId();
        var years    = lookup.GetYears();
        var classes  = lookup.GetClasses();
        var sections = lookup.GetSections();
        var batches  = lookup.GetBatches();
        var examList = svc.GetExamList(yearId, classId);
        var students = classId.HasValue ? svc.GetStudentsForEntry(classId, sectionId, batchId, yearId) : new List<StudentMarkRow>();

        var data = new List<TestMark>();
        if (classId.HasValue && !string.IsNullOrWhiteSpace(examName))
            data = svc.GetTestResult(examName, yearId.Value, classId, sectionId, batchId, studentId, testDate);

        ViewBag.Years     = years;
        ViewBag.Classes   = classes;
        ViewBag.Sections  = sections;
        ViewBag.Batches   = batches;
        ViewBag.ExamList  = examList;
        ViewBag.Students  = students;
        ViewBag.YearId    = yearId;
        ViewBag.ClassId   = classId;
        ViewBag.SectionId = sectionId;
        ViewBag.BatchId   = batchId;
        ViewBag.StudentId = studentId;
        ViewBag.ExamName  = examName ?? "";
        ViewBag.TestDate  = testDate?.ToString("yyyy-MM-dd") ?? "";
        return View(data);
    }

    public IActionResult PrintResult(string examName, int? yearId, int? classId, int? sectionId,
        int? batchId, int? studentId, DateTime? testDate)
    {
        int yr = yearId ?? lookup.GetCurrentYearId() ?? 1;
        var result = svc.GetTestResult(examName, yr, classId, sectionId, batchId, studentId, testDate);
        ViewBag.ExamName  = examName;
        ViewBag.TestDate  = testDate?.ToString("dd MMM yyyy") ?? "";
        ViewBag.ClassId   = classId;
        ViewBag.StudentId = studentId;
        return View(result);
    }

    public IActionResult DownloadReport(string examName, int? yearId, int? classId,
        int? sectionId, int? batchId, DateTime? testDate)
    {
        int yr = yearId ?? lookup.GetCurrentYearId() ?? 1;
        var data = svc.GetTestResult(examName, yr, classId, sectionId, batchId, null, testDate);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Roll No,Student Name,Admission No,Class,Section,Batch,Subject,Marks Obtained,Max Marks,%,Grade,Result");
        var byStudent = data.GroupBy(m => m.StudentId);
        foreach (var sg in byStudent.OrderBy(g => g.First().RollNo))
        {
            var f = sg.First();
            foreach (var mk in sg.OrderBy(m => m.SubjectName))
            {
                var pct = mk.MaxMarks > 0 && mk.MarksObtained.HasValue
                    ? (mk.MarksObtained.Value * 100 / mk.MaxMarks).ToString("0")
                    : "AB";
                var result2 = mk.Grade == "AB" ? "ABSENT" : (mk.MarksObtained.HasValue && mk.MarksObtained.Value * 100 / mk.MaxMarks >= 35 ? "PASS" : "FAIL");
            }
        }
        var fileName = $"TestReport_{examName}_{testDate?.ToString("ddMMMyyyy") ?? "NoDate"}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }
}

public class MarksSaveReq
{
    public string    ExamName{get;set;}="";
    public int       ClassId{get;set;}
    public int       YearId{get;set;}
    public int       MaxMarks{get;set;}=30;
    public DateTime? TestDate{get;set;}
    public List<MarkEntry> Entries{get;set;}=new();
}
public class MarkEntry { public int StudentId{get;set;} public int SubjectId{get;set;} public decimal Marks{get;set;} public bool IsAbsent{get;set;}=false; }
