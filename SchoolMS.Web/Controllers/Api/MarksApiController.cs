using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Controllers;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/marks")]
[ApiRequireStaff]
public class MarksApiController(MarksService svc, LookupService lookup, MastersService mastersSvc) : ControllerBase
{
    [HttpGet]
    [ApiRequirePermission("marks_entry")]
    public IActionResult Index(int? yearId, int? classId, int? sectionId, int? batchId,
        int? subjectId, string? examName, DateTime? testDate, int maxMarks = 30)
    {
        yearId ??= lookup.GetCurrentYearId();
        testDate ??= DateTime.Today;

        var subjects = mastersSvc.GetSubjects();
        var examList = svc.GetExamList(yearId, null);

        if (subjectId == null && subjects.Count == 1) subjectId = subjects[0].SubjectId;

        var students = new List<StudentMarkRow>();
        if (classId.HasValue && subjectId.HasValue && !string.IsNullOrWhiteSpace(examName))
            students = svc.GetStudentsWithMarks(classId, sectionId, batchId, subjectId.Value, examName, testDate, yearId);
        else if (classId.HasValue)
            students = svc.GetStudentsForEntry(classId, sectionId, batchId, yearId);

        return Ok(new {
            students,
            subjects,
            examList,
            yearId,
            subjectId,
            examName = examName ?? "",
            testDate = testDate.Value.ToString("yyyy-MM-dd"),
            maxMarks,
        });
    }

    [HttpPost("save")]
    [ApiRequirePermission("marks_entry")]
    public IActionResult Save([FromBody] MarksSaveReq req)
    {
        try
        {
            int uid = User.UserId() ?? 1;
            int yr = req.YearId > 0 ? req.YearId : (lookup.GetCurrentYearId() ?? 1);
            DateTime? testDate = req.TestDate.HasValue ? req.TestDate.Value.Date : (DateTime?)null;
            foreach (var e in req.Entries)
                svc.SaveMarkDirect(e.StudentId, e.SubjectId, req.ExamName, yr, req.ClassId,
                    testDate, e.IsAbsent ? null : e.Marks, req.MaxMarks, e.IsAbsent, uid);
            return Ok(new { success = true, message = $"Marks saved for {req.Entries.Count} students." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("report")]
    [ApiRequirePermission("marks_report")]
    public IActionResult Report(int? yearId)
    {
        yearId ??= lookup.GetCurrentYearId();
        return Ok(new { yearId, years = lookup.GetYears() });
    }

    [HttpGet("subjects-with-tests")]
    [ApiRequirePermission("marks_report")]
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

            return Ok(subjects);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("tests-for-subject")]
    [ApiRequirePermission("marks_report")]
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

            return Ok(tests);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("test-marks-detailed")]
    [ApiRequirePermission("marks_report")]
    public IActionResult GetTestMarksDetailed(string examName, int classId, int subjectId, int yearId)
    {
        try
        {
            var marks = svc.GetTestResult(examName, yearId, classId, null, null, null, null)
                .Where(m => m.SubjectId == subjectId)
                .OrderByDescending(m => m.MarksObtained)
                .ToList();

            return Ok(new {
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

    [HttpGet("top-students")]
    [ApiRequirePermission("marks_topstudents")]
    public IActionResult TopStudents(int? yearId, int? classId, int? sectionId)
    {
        yearId ??= lookup.GetCurrentYearId();

        var raw = svc.GetStudentRankings(yearId, classId, sectionId);

        var overallTop5 = raw
            .GroupBy(x => x.StudentId)
            .Select(g => new TopStudent {
                StudentId = g.Key,
                FullName = g.First().FullName,
                AdmissionNo = g.First().AdmissionNo,
                RollNo = g.First().RollNo,
                ClassName = g.First().ClassName,
                SectionName = g.First().SectionName,
                BatchName = g.First().BatchName,
                YearName = g.First().YearName,
                TotalObtained = g.Sum(x => x.TotalObtained),
                TotalMax = g.Sum(x => x.TotalMax),
                Percentage = g.Sum(x => x.TotalMax) > 0
                    ? Math.Round(g.Sum(x => x.TotalObtained) * 100m / g.Sum(x => x.TotalMax), 2)
                    : 0
            })
            .OrderByDescending(x => x.Percentage)
            .ThenByDescending(x => x.TotalObtained)
            .Take(5)
            .Select((x, i) => { x.Rank = i + 1; return x; })
            .ToList();

        var classwiseTop5 = raw
            .GroupBy(x => (x.StudentId, x.ClassName))
            .Select(g => new TopStudent {
                StudentId = g.Key.StudentId,
                FullName = g.First().FullName,
                AdmissionNo = g.First().AdmissionNo,
                RollNo = g.First().RollNo,
                ClassName = g.Key.ClassName,
                SectionName = g.First().SectionName,
                BatchName = g.First().BatchName,
                YearName = g.First().YearName,
                TotalObtained = g.Sum(x => x.TotalObtained),
                TotalMax = g.Sum(x => x.TotalMax),
                Percentage = g.Sum(x => x.TotalMax) > 0
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

        var subjectRaw = svc.GetSubjectWiseRankings(yearId, classId, sectionId);
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

        var allMarksRaw = classId.HasValue || sectionId.HasValue
            ? svc.GetAllMarksForClass(classId, sectionId, null, yearId)
            : new List<TestMark>();
        var examSubjectMap = allMarksRaw
            .Where(m => m.MarksObtained.HasValue)
            .GroupBy(m => m.ExamName ?? "")
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(m => m.StudentId)
                      .ToDictionary(sg => sg.Key, sg => sg.ToList())
            );

        return Ok(new {
            yearId,
            classId,
            sectionId,
            overallTop5,
            classwiseTop5,
            testwiseTop5,
            subjectwiseTop5,
            examSubjectMap,
        });
    }

    [HttpGet("date-wise-tests")]
    [ApiRequirePermission("marks_report")]
    public IActionResult GetDateWiseTests(string date, int? classId, int? sectionId, int? batchId)
    {
        if (!DateTime.TryParse(date, out var selectedDate))
            return Ok(new List<object>());

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

        return Ok(result);
    }
}
