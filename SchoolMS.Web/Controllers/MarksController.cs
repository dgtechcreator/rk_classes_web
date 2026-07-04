using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class MarksController(MarksService svc, LookupService lookup, MastersService mastersSvc) : Controller
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
        var subjects = mastersSvc.GetSubjects();

        // Get all exams for the year (no class filter)
        var examList = svc.GetExamList(yearId, null);

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

    public IActionResult Report(int? yearId)
    {
        yearId ??= lookup.GetCurrentYearId();
        var years = lookup.GetYears();

        ViewBag.YearId = yearId;
        ViewBag.Years = years;
        return View();
    }

    [HttpGet]
    public IActionResult GetSubjectsWithTests(int yearId)
    {
        try
        {
            var allTests = svc.GetAllTestsByYear(yearId);
            var subjects = allTests
                .GroupBy(t => new { t.SubjectId, t.SubjectName })
                .Select(g => new {
                    subjectId = g.Key.SubjectId,
                    subjectName = g.Key.SubjectName,
                    testCount = g.Select(t => t.ExamId).Distinct().Count()
                })
                .OrderBy(s => s.subjectName)
                .ToList();

            return Json(subjects);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetTestsForSubject(int subjectId, int yearId)
    {
        try
        {
            var allTests = svc.GetAllTestsByYear(yearId);
            var tests = allTests
                .Where(t => t.SubjectId == subjectId)
                .GroupBy(t => new { t.ExamId, t.ExamName, t.TestDate, t.ClassId, t.ClassName })
                .Select(g => new {
                    examId = g.Key.ExamId,
                    examName = g.Key.ExamName,
                    testDate = g.Key.TestDate?.ToString("dd MMM yyyy"),
                    classId = g.Key.ClassId,
                    className = g.Key.ClassName,
                    studentCount = g.Select(x => x.StudentId).Distinct().Count()
                })
                .OrderByDescending(t => t.testDate)
                .ToList();

            return Json(tests);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetTestMarksDetailed(string examName, int classId, int subjectId, int yearId)
    {
        try
        {
            var marks = svc.GetTestResult(examName, yearId, classId, null, null, null, null)
                .Where(m => m.SubjectId == subjectId)
                .OrderByDescending(m => m.MarksObtained)
                .ToList();

            return Json(new {
                marks = marks.Select(m => new {
                    studentId = m.StudentId,
                    fullName = m.FullName,
                    className = m.ClassName,
                    batchName = m.BatchName,
                    obtainedMarks = m.MarksObtained,
                    maxMarks = m.MaxMarks,
                    grade = m.Grade
                }).ToList(),
                testInfo = marks.FirstOrDefault() != null ? new {
                    examName = marks.First().ExamName,
                    subjectName = marks.First().SubjectName,
                    className = marks.First().ClassName,
                    testDate = marks.First().TestDate?.ToString("dd MMM yyyy")
                } : null
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public IActionResult TopStudents(int? yearId, int? classId, int? sectionId)
    {
        yearId ??= lookup.GetCurrentYearId();
        var years    = lookup.GetYears();
        var classes  = lookup.GetClasses();
        var sections = lookup.GetSections();

        // Fetch raw per-student per-exam aggregates
        var raw = svc.GetStudentRankings(yearId, classId, sectionId);

        // ── 1. Overall Top 5 (all exams combined) ──────────────────────
        var overallTop5 = raw
            .GroupBy(x => x.StudentId)
            .Select(g => new TopStudent {
                StudentId     = g.Key,
                FullName      = g.First().FullName,
                AdmissionNo   = g.First().AdmissionNo,
                RollNo        = g.First().RollNo,
                ClassName     = g.First().ClassName,
                SectionName   = g.First().SectionName,
                BatchName     = g.First().BatchName,
                YearName      = g.First().YearName,
                TotalObtained = g.Sum(x => x.TotalObtained),
                TotalMax      = g.Sum(x => x.TotalMax),
                Percentage    = g.Sum(x => x.TotalMax) > 0
                    ? Math.Round(g.Sum(x => x.TotalObtained) * 100m / g.Sum(x => x.TotalMax), 2)
                    : 0
            })
            .OrderByDescending(x => x.Percentage)
            .ThenByDescending(x => x.TotalObtained)
            .Take(5)
            .Select((x, i) => { x.Rank = i + 1; return x; })
            .ToList();

        // ── 2. Class-wise Top 5 ─────────────────────────────────────────
        var classwiseTop5 = raw
            .GroupBy(x => (x.StudentId, x.ClassName))
            .Select(g => new TopStudent {
                StudentId     = g.Key.StudentId,
                FullName      = g.First().FullName,
                AdmissionNo   = g.First().AdmissionNo,
                RollNo        = g.First().RollNo,
                ClassName     = g.Key.ClassName,
                SectionName   = g.First().SectionName,
                BatchName     = g.First().BatchName,
                YearName      = g.First().YearName,
                TotalObtained = g.Sum(x => x.TotalObtained),
                TotalMax      = g.Sum(x => x.TotalMax),
                Percentage    = g.Sum(x => x.TotalMax) > 0
                    ? Math.Round(g.Sum(x => x.TotalObtained) * 100m / g.Sum(x => x.TotalMax), 2)
                    : 0
            })
            .GroupBy(x => x.ClassName ?? "")
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Percentage)
                      .ThenByDescending(x => x.TotalObtained)
                      .Take(5)
                      .Select((x, i) => { x.Rank = i + 1; return x; })
                      .ToList()
            );

        // ── 3. Test-wise Top 5 (per exam, top 5 students) ──────────────
        var testwiseTop5 = raw
            .GroupBy(x => x.ExamName ?? "")
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Percentage)
                      .ThenByDescending(x => x.TotalObtained)
                      .Take(5)
                      .Select((x, i) => { x.Rank = i + 1; return x; })
                      .ToList()
            );

        // ── 4. Subject-wise Top 5 (per subject, top 5 students) ────────
        var subjectRaw     = svc.GetSubjectWiseRankings(yearId, classId, sectionId);
        var subjectwiseTop5 = subjectRaw
            .GroupBy(x => x.SubjectName ?? "")
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Percentage)
                      .ThenByDescending(x => x.TotalObtained)
                      .Take(5)
                      .Select((x, i) => { x.Rank = i + 1; return x; })
                      .ToList()
            );

        // ── Test-wise: also attach subject breakdown per student ────────
        // For each exam, which subjects and marks each top student has
        var allMarksRaw = classId.HasValue || sectionId.HasValue
            ? svc.GetAllMarksForClass(classId, sectionId, null, yearId)
            : new List<TestMark>();
        // Dictionary: examName -> studentId -> list of subject marks
        var examSubjectMap = allMarksRaw
            .Where(m => m.MarksObtained.HasValue)
            .GroupBy(m => m.ExamName ?? "")
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(m => m.StudentId)
                      .ToDictionary(sg => sg.Key, sg => sg.ToList())
            );

        ViewBag.Years            = years;
        ViewBag.Classes          = classes;
        ViewBag.Sections         = sections;
        ViewBag.YearId           = yearId;
        ViewBag.ClassId          = classId;
        ViewBag.SectionId        = sectionId;
        ViewBag.OverallTop5      = overallTop5;
        ViewBag.ClasswiseTop5    = classwiseTop5;
        ViewBag.TestwiseTop5     = testwiseTop5;
        ViewBag.SubjectwiseTop5  = subjectwiseTop5;
        ViewBag.ExamSubjectMap   = examSubjectMap;
        return View();
    }

    public IActionResult PrintResult(string examName, int? yearId, int? classId, int? sectionId,
        int? batchId, int? studentId, DateTime? testDate)
    {
        var result = DedupeMarks(svc.GetTestResult(examName, yearId ?? 0, classId, sectionId, batchId, studentId, null));
        ViewBag.ExamName  = examName;
        ViewBag.TestDate  = testDate?.ToString("dd MMM yyyy") ?? "";
        ViewBag.ClassId   = classId;
        ViewBag.StudentId = studentId;
        return View(result);
    }

    public IActionResult DownloadReport(string examName, int? yearId, int? classId,
        int? sectionId, int? batchId, DateTime? testDate)
    {
        var data = DedupeMarks(svc.GetTestResult(examName, yearId ?? 0, classId, sectionId, batchId, null, null));
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Roll No,Student Name,Admission No,Class,Section,Batch,Subject,Marks Obtained,Max Marks,%,Grade,Result");
        foreach (var sg in data.GroupBy(m => m.StudentId).OrderBy(g => g.First().RollNo))
        {
            foreach (var mk in sg.OrderBy(m => m.SubjectName))
            {
                var pct = mk.MaxMarks > 0 && mk.MarksObtained.HasValue
                    ? (mk.MarksObtained.Value * 100 / mk.MaxMarks).ToString("0")
                    : "AB";
                var result2 = mk.Grade == "AB" ? "ABSENT"
                    : (mk.MarksObtained.HasValue && mk.MarksObtained.Value * 100 / mk.MaxMarks >= 35 ? "PASS" : "FAIL");
                sb.AppendLine(string.Join(",",
                    Csv(mk.RollNo), Csv(mk.FullName), Csv(mk.AdmissionNo),
                    Csv(mk.ClassName), Csv(mk.SectionName), Csv(mk.BatchName), Csv(mk.SubjectName),
                    mk.Grade == "AB" ? "AB" : mk.MarksObtained?.ToString("0") ?? "",
                    mk.MaxMarks.ToString(), pct, Csv(mk.Grade), result2));
            }
        }
        var fileName = $"TestReport_{examName}_{testDate?.ToString("ddMMMyyyy") ?? "NoDate"}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    static string Csv(string? v) =>
        v == null ? "" : (v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v);

    // When the old SP returns duplicate rows per student+subject (one per duplicate exam),
    // keep the best row: actual marks > absent(AB) > anything else.
    static List<TestMark> DedupeMarks(List<TestMark> rows) =>
        rows.GroupBy(m => (m.StudentId, m.SubjectId))
            .Select(g =>
                g.FirstOrDefault(x => x.MarksObtained.HasValue)   // row with real marks wins
                ?? g.FirstOrDefault(x => x.Grade == "AB")          // then AB (absent)
                ?? g.First())                                       // fallback
            .ToList();

    [HttpGet]
    public IActionResult GetDateWiseTests(string date, int? classId, int? sectionId, int? batchId)
    {
        if (!DateTime.TryParse(date, out var selectedDate))
            return Json(new List<object>());

        var allMarks = svc.GetAllMarksForClass(classId, sectionId, batchId, null);
        var result = allMarks
            .Where(m => m.EnteredAt.HasValue && m.EnteredAt.Value.Date == selectedDate.Date)
            .GroupBy(m => new { m.ExamId, m.ExamName, m.ClassName, m.SectionName, m.BatchName })
            .Select(g => new {
                examName = g.Key.ExamName ?? "",
                className = g.Key.ClassName ?? "",
                sectionName = g.Key.SectionName ?? "",
                batchName = g.Key.BatchName ?? "",
                testDate = selectedDate.ToString("dd MMM yyyy"),
                entryCount = g.Count(),
                classId = g.First().ClassId,
                sectionId = g.First().SectionId,
                batchId = g.First().BatchId
            })
            .Distinct()
            .ToList();

        return Json(result);
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
