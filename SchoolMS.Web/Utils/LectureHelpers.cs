using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Utils;

/// <summary>
/// JSON payloads for the lecture module, shared by the web controller (session login) and the mobile API
/// (JWT) so both always return exactly the same numbers.
/// </summary>
public static class LectureHelpers
{
    public static object Dto(Lecture l, DateTime now) => new
    {
        lectureId = l.LectureId,
        date = l.LectureDate.ToString("yyyy-MM-dd"),
        startTime = l.StartTime.ToString(@"hh\:mm"),
        endTime = l.EndTime.ToString(@"hh\:mm"),
        classId = l.ClassId, className = l.ClassName?.Trim(),
        sectionId = l.SectionId, sectionName = l.SectionName?.Trim(),
        batchId = l.BatchId, batchName = l.BatchName?.Trim(),
        groupLabel = l.GroupLabel,
        subject = l.SubjectName.Trim(),
        facultyId = l.FacultyId, teacher = l.FacultyName?.Trim(),
        topic = l.Topic, remarks = l.Remarks,
        status = l.Status, statusNote = l.StatusNote,
        isRepeating = l.SeriesId != null,
        // Scheduled but its time has gone by without being marked done / cancelled.
        needsUpdate = l.Status == LectureStatus.Scheduled && LectureService.IsPast(l, now),
    };

    /// <summary>Clamps a requested range: defaults to the current month, never more than a year, from &lt;= to.</summary>
    public static (DateTime from, DateTime to) Range(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var f = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var t = (to ?? f.AddMonths(1).AddDays(-1)).Date;
        if (t < f) t = f;
        if ((t - f).TotalDays > 366) t = f.AddDays(366);
        return (f, t);
    }

    public static (DateTime from, DateTime to) MonthRange(DateTime anyDayInMonth)
    {
        var f = new DateTime(anyDayInMonth.Year, anyDayInMonth.Month, 1);
        return (f, f.AddMonths(1).AddDays(-1));
    }

    public static object Totals(IEnumerable<Lecture> ls, DateTime now)
    {
        var list = ls as IList<Lecture> ?? ls.ToList();
        return new
        {
            total = list.Count,
            held = list.Count(l => l.Status == LectureStatus.Completed),
            scheduled = list.Count(l => l.Status == LectureStatus.Scheduled),
            cancelled = list.Count(l => l.Status == LectureStatus.Cancelled),
            notUpdated = list.Count(l => l.Status == LectureStatus.Scheduled && LectureService.IsPast(l, now)),
            hoursHeld = Math.Round(list.Where(l => l.Status == LectureStatus.Completed).Sum(l => l.DurationHours), 2),
        };
    }

    public static object ListPayload(LectureService svc, LectureFilter f)
    {
        var now = DateTime.Now;
        var list = svc.Search(f);
        return new
        {
            from = f.From?.ToString("yyyy-MM-dd"), to = f.To?.ToString("yyyy-MM-dd"),
            totals = Totals(list, now),
            lectures = list.Select(l => Dto(l, now)),
        };
    }

    public static object SummaryPayload(LectureService svc, LectureFilter f, string? groupBy)
    {
        var now = DateTime.Now;
        var kind = (groupBy ?? "teacher").ToLowerInvariant() is "class" or "subject" ? groupBy!.ToLowerInvariant() : "teacher";
        var list = svc.Search(f);
        return new
        {
            from = f.From?.ToString("yyyy-MM-dd"), to = f.To?.ToString("yyyy-MM-dd"), groupBy = kind,
            totals = Totals(list, now),
            rows = LectureService.Summarize(list, kind, now).Select(r => new
            {
                key = r.Key, label = r.Label, facultyId = r.FacultyId,
                scheduled = r.Scheduled, completed = r.Completed, cancelled = r.Cancelled, total = r.Total,
                hoursHeld = r.HoursHeld, notUpdated = r.NotUpdated,
            }),
        };
    }

    public static object OptionsPayload(LookupService lookup, MastersService masters, FacultyService faculty)
    {
        var (teachers, _) = faculty.GetAll(null, "Active", null, 1, 1000);
        return new
        {
            classes = lookup.GetClasses().Select(c => new { id = c.ClassId, name = c.ClassName.Trim() }),
            sections = lookup.GetSections().Select(s => new { id = s.SectionId, name = s.SectionName.Trim() }),
            batches = lookup.GetBatches().Select(b => new { id = b.BatchId, name = b.BatchName.Trim() }),
            subjects = masters.GetSubjects().Select(s => s.SubjectName.Trim()).Where(n => n != "").Distinct().OrderBy(n => n),
            teachers = teachers.Select(t => new { id = t.FacultyId, name = t.FullName.Trim(), displayName = t.FullName.Trim() }),
        };
    }

    static object Block(string label, DateTime from, DateTime to, List<Lecture> list, DateTime now) => new
    {
        label, from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"),
        totals = Totals(list, now),
        byGroup = LectureService.Summarize(list, "class", now).Select(r => new { label = r.Label, held = r.Completed, scheduled = r.Scheduled, cancelled = r.Cancelled }),
        bySubject = LectureService.Summarize(list, "subject", now).Select(r => new { label = r.Label, held = r.Completed, scheduled = r.Scheduled, cancelled = r.Cancelled }),
    };

    /// <summary>A teacher's own lectures in a range plus this-month / last-month counts.</summary>
    public static object MinePayload(LectureService svc, Faculty faculty, DateTime from, DateTime to)
    {
        var now = DateTime.Now;
        var thisM = MonthRange(now);
        var lastM = MonthRange(now.AddMonths(-1));
        var inRange = svc.Search(new LectureFilter { FacultyId = faculty.FacultyId, From = from, To = to });
        var month = svc.Search(new LectureFilter { FacultyId = faculty.FacultyId, From = lastM.Item1, To = thisM.Item2 });
        return new
        {
            linked = true,
            facultyId = faculty.FacultyId,
            facultyName = faculty.FullName.Trim(),
            from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd"),
            lectures = inRange.Select(l => Dto(l, now)),
            thisMonth = Block(now.ToString("MMMM yyyy"), thisM.Item1, thisM.Item2,
                month.Where(l => l.LectureDate.Date >= thisM.Item1).ToList(), now),
            lastMonth = Block(now.AddMonths(-1).ToString("MMMM yyyy"), lastM.Item1, lastM.Item2,
                month.Where(l => l.LectureDate.Date <= lastM.Item2).ToList(), now),
        };
    }

    /// <summary>What a parent sees for one child: that day's lectures + the counts for the day's month.</summary>
    public static object ParentPayload(LectureService svc, Student child, DateTime date)
    {
        var now = DateTime.Now;
        date = date.Date;
        var (mFrom, mTo) = MonthRange(date);
        if (child.ClassId == null)
            return new { date = date.ToString("yyyy-MM-dd"), lectures = Array.Empty<object>(), month = (object?)null, monthDays = Array.Empty<object>() };

        var forChild = new LectureFilter
        {
            From = mFrom, To = mTo,
            ForStudentClassId = child.ClassId, ForStudentSectionId = child.SectionId, ForStudentBatchId = child.BatchId,
        };
        var month = svc.Search(forChild);
        var day = month.Where(l => l.LectureDate.Date == date).ToList();

        return new
        {
            date = date.ToString("yyyy-MM-dd"),
            lectures = day.Select(l => Dto(l, now)),
            month = Block(date.ToString("MMMM yyyy"), mFrom, mTo, month, now),
            monthDays = month.GroupBy(l => l.LectureDate.Date).OrderBy(g => g.Key)
                .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Count(l => l.Status != LectureStatus.Cancelled) }),
        };
    }
}
