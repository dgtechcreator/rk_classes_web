using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Utils;

/// <summary>
/// "All paper list" for a parent / student: every paper (exam + subject) the child's class & medium has marks for,
/// grouped by subject (newest first). Each paper carries the child's own marks (or Absent) and the Top 5 of that
/// paper in the class & medium. The web parent portal and the mobile API both call this, so they cannot drift apart.
/// Ranking is by marks for that one paper; ties share a rank. Only name, marks and percentage are exposed.
/// </summary>
public static class ParentPapersHelper
{
    public static object Build(Student child, MarksService marksSvc, LookupService lookup)
    {
        if (child.ClassId == null) return new { subjects = Array.Empty<object>() };

        int? yearId = lookup.GetCurrentYearId();
        var rows = marksSvc.GetAllMarksForClass(child.ClassId, child.SectionId, null, yearId);
        if (rows.Count == 0 && yearId != null)
        {
            // Same fallback as the toppers page: a class that only has older exams still shows its papers.
            yearId = null;
            rows = marksSvc.GetAllMarksForClass(child.ClassId, child.SectionId, null, null);
        }

        var papers = rows
            .GroupBy(m => (m.ExamId, m.SubjectId))
            .Select(g => BuildPaper(g.ToList(), child.StudentId))
            .OrderBy(p => p.SubjectName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(p => p.TestDate ?? DateTime.MinValue)
            .ThenBy(p => p.ExamName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var subjects = papers
            .GroupBy(p => p.SubjectName)
            .Select(g => new {
                subjectName = g.Key,
                papers = g.Select(p => new {
                    examId = p.ExamId,
                    subjectId = p.SubjectId,
                    examName = p.ExamName,
                    testDate = p.TestDate,
                    maxMarks = p.MaxMarks,
                    classSize = p.SatCount,
                    my = new {
                        marks = p.MyMarks,
                        absent = p.MyAbsent,
                        percentage = p.MyPercentage,
                        rank = p.MyRank,
                    },
                    top = p.Top,
                }),
            });

        return new {
            className = child.ClassName,
            sectionName = child.SectionName,
            yearName = rows.FirstOrDefault()?.YearName,
            subjects,
        };
    }

    private sealed record PaperRow(
        int ExamId, int SubjectId, string SubjectName, string ExamName, DateTime? TestDate, int MaxMarks,
        int SatCount, decimal? MyMarks, bool MyAbsent, decimal? MyPercentage, int? MyRank, string? YearName,
        IReadOnlyList<object> Top);

    private static PaperRow BuildPaper(List<TestMark> group, int myStudentId)
    {
        var first = group[0];
        int max = group.Max(m => m.MaxMarks);

        // Students who sat the paper, best first. Null marks = absent (not counted in the ranking).
        var sat = group.Where(m => m.MarksObtained.HasValue)
            .OrderByDescending(m => m.MarksObtained)
            .ThenBy(m => m.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Competition ranking: equal marks share a rank (1, 1, 3 ...).
        var ranked = new List<(TestMark M, int Rank)>(sat.Count);
        for (int i = 0; i < sat.Count; i++)
        {
            int rank = i > 0 && sat[i].MarksObtained == sat[i - 1].MarksObtained ? ranked[i - 1].Rank : i + 1;
            ranked.Add((sat[i], rank));
        }

        var mine = group.FirstOrDefault(m => m.StudentId == myStudentId);
        var myRanked = ranked.FirstOrDefault(x => x.M.StudentId == myStudentId);

        var top = ranked.Where(x => x.Rank <= 5).Select(x => (object)new {
            rank = x.Rank,
            fullName = x.M.FullName,
            marks = x.M.MarksObtained,
            percentage = Pct(x.M.MarksObtained, max),
            isMe = x.M.StudentId == myStudentId,
        }).ToList();

        return new PaperRow(
            ExamId: first.ExamId,
            SubjectId: first.SubjectId,
            SubjectName: string.IsNullOrWhiteSpace(first.SubjectName) ? "Subject" : first.SubjectName!,
            ExamName: first.ExamName ?? "Test",
            TestDate: first.TestDate,
            MaxMarks: max,
            SatCount: sat.Count,
            MyMarks: mine?.MarksObtained,
            MyAbsent: mine != null && !mine.MarksObtained.HasValue,
            MyPercentage: mine?.MarksObtained.HasValue == true ? Pct(mine.MarksObtained, max) : null,
            MyRank: myRanked.M != null ? myRanked.Rank : (int?)null,
            YearName: first.YearName,
            Top: top);
    }

    private static decimal? Pct(decimal? marks, int max) =>
        marks.HasValue && max > 0 ? Math.Round(marks.Value * 100m / max, 1) : null;
}
