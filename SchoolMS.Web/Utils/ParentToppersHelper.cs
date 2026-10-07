using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Utils;

/// <summary>
/// "Class toppers" shown to a parent: the Top 5 (overall and per subject) of THEIR child's own class and
/// medium only, plus where the child stands. Uses the same ranking queries as the admin Top Students
/// page (<see cref="MarksService.GetStudentRankings"/> / GetSubjectWiseRankings), so parent and admin
/// always see the same numbers. Only name, marks and percentage are exposed — no admission/roll numbers.
/// The web parent portal and the mobile API both call this, so they cannot drift apart.
/// </summary>
public static class ParentToppersHelper
{
    public static object Build(Student child, MarksService marksSvc, LookupService lookup)
    {
        if (child.ClassId == null) return Empty(child);

        // Current academic year first (same default as the admin page); fall back to all years so a
        // class that only has older exams still shows its toppers.
        int? yearId = lookup.GetCurrentYearId();
        var raw = marksSvc.GetStudentRankings(yearId, child.ClassId, child.SectionId);
        var subjectRaw = marksSvc.GetSubjectWiseRankings(yearId, child.ClassId, child.SectionId);
        if (raw.Count == 0 && subjectRaw.Count == 0)
        {
            yearId = null;
            raw = marksSvc.GetStudentRankings(null, child.ClassId, child.SectionId);
            subjectRaw = marksSvc.GetSubjectWiseRankings(null, child.ClassId, child.SectionId);
        }

        var overall = Rank(raw
            .GroupBy(x => x.StudentId)
            .Select(g => new TopStudent {
                StudentId = g.Key,
                FullName = g.First().FullName,
                YearName = g.First().YearName,
                TotalObtained = g.Sum(x => x.TotalObtained),
                TotalMax = g.Sum(x => x.TotalMax),
                Percentage = g.Sum(x => x.TotalMax) > 0
                    ? Math.Round(g.Sum(x => x.TotalObtained) * 100m / g.Sum(x => x.TotalMax), 2) : 0,
            }));

        var subjects = subjectRaw
            .GroupBy(x => x.SubjectName ?? "")
            .Where(g => g.Key != "")
            .OrderBy(g => g.Key)
            .Select(g => (name: g.Key, ranked: Rank(g)))
            .ToList();

        return new {
            className = child.ClassName,
            sectionName = child.SectionName,
            yearName = raw.FirstOrDefault()?.YearName ?? subjectRaw.FirstOrDefault()?.YearName,
            overall = Section(overall, child.StudentId),
            subjects = subjects.Select(s => new {
                subjectName = s.name,
                top = Section(s.ranked, child.StudentId),
            }),
        };
    }

    private static object Empty(Student child) => new {
        className = child.ClassName, sectionName = child.SectionName, yearName = (string?)null,
        overall = Section(new List<TopStudent>(), child.StudentId), subjects = Array.Empty<object>(),
    };

    // Highest percentage first, ties broken by total marks (like the admin page). Students with exactly the
    // same percentage AND marks share a rank (1, 1, 1, 4 ...) so nobody is shown "below" an equal scorer.
    private static List<TopStudent> Rank(IEnumerable<TopStudent> rows)
    {
        var list = rows
            .OrderByDescending(x => x.Percentage)
            .ThenByDescending(x => x.TotalObtained)
            .ThenBy(x => x.FullName)
            .ToList();
        for (int i = 0; i < list.Count; i++)
            list[i].Rank = i > 0 && list[i].Percentage == list[i - 1].Percentage && list[i].TotalObtained == list[i - 1].TotalObtained
                ? list[i - 1].Rank
                : i + 1;
        return list;
    }

    private static object Section(List<TopStudent> ranked, int myStudentId)
    {
        var me = ranked.FirstOrDefault(x => x.StudentId == myStudentId);
        return new {
            ranked = ranked.Count,
            myRank = me?.Rank,
            myPercentage = me?.Percentage,
            myObtained = me?.TotalObtained,
            myMax = me?.TotalMax,
            top = ranked.Take(5).Select(x => new {
                rank = x.Rank,
                fullName = x.FullName,
                percentage = x.Percentage,
                totalObtained = x.TotalObtained,
                totalMax = x.TotalMax,
                isMe = x.StudentId == myStudentId,
            }),
        };
    }
}
