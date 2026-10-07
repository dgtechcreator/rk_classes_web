using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// Lecture schedule rules: validation, weekly-repeat expansion, clash detection (same teacher, or same
/// class/medium/batch at the same time) and the counts shown in the summaries. A lecture only counts as
/// "held" once it is marked Completed — Scheduled and Cancelled are reported separately so nothing is hidden.
/// </summary>
public class LectureService(LectureRepo repo)
{
    public const int MaxOccurrences = 400;

    public List<Lecture> Search(LectureFilter f) => repo.Search(f);
    public Lecture? GetById(int id) => repo.GetById(id);

    // ── Pure helpers (unit-testable, no database) ────────────────────────────────────────────

    public static bool TryParseTime(string? s, out TimeSpan t)
    {
        t = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (!TimeSpan.TryParse(s.Trim(), out t)) return false;
        t = new TimeSpan(t.Hours, t.Minutes, 0);
        return t >= TimeSpan.Zero && t < TimeSpan.FromDays(1);
    }

    /// <summary>The dates a request produces: just the date, or every chosen weekday from the date up to RepeatUntil.</summary>
    public static (string? error, List<DateTime> dates) ExpandDates(LectureSaveReq r)
    {
        var start = r.Date.Date;
        if (!r.Repeat) return (null, new List<DateTime> { start });

        var days = r.RepeatDays?.Where(d => d is >= 0 and <= 6).Distinct().ToHashSet() ?? new HashSet<int>();
        if (days.Count == 0) return ("Pick at least one weekday to repeat on.", new());
        if (r.RepeatUntil == null) return ("Choose the date until which the lecture repeats.", new());
        var until = r.RepeatUntil.Value.Date;
        if (until < start) return ("'Repeat until' cannot be before the first lecture date.", new());
        if ((until - start).TotalDays > 366) return ("A repeating schedule can cover at most one year.", new());

        var dates = new List<DateTime>();
        for (var d = start; d <= until; d = d.AddDays(1))
            if (days.Contains((int)d.DayOfWeek)) dates.Add(d);
        if (dates.Count == 0) return ("No lecture falls on the chosen weekdays in that period.", new());
        if (dates.Count > MaxOccurrences) return ($"That would create {dates.Count} lectures; the limit is {MaxOccurrences} at a time.", new());
        return (null, dates);
    }

    static bool TimesOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd) => aStart < bEnd && bStart < aEnd;

    // Two lectures target the same students when class matches and medium / batch are equal or either side means "all".
    static bool SameStudents(Lecture a, Lecture b)
        => a.ClassId == b.ClassId
           && (a.SectionId == null || b.SectionId == null || a.SectionId == b.SectionId)
           && (a.BatchId == null || b.BatchId == null || a.BatchId == b.BatchId);

    /// <summary>A human message when the candidate clashes with an existing (non-cancelled) lecture, else null.</summary>
    public static string? FindConflict(Lecture cand, IEnumerable<Lecture> existing)
    {
        foreach (var e in existing)
        {
            if (e.LectureId == cand.LectureId && cand.LectureId != 0) continue;
            if (e.Status == LectureStatus.Cancelled) continue;
            if (e.LectureDate.Date != cand.LectureDate.Date) continue;
            if (!TimesOverlap(cand.StartTime, cand.EndTime, e.StartTime, e.EndTime)) continue;

            var when = $"{e.LectureDate:dd MMM}, {e.StartTime:hh\\:mm}-{e.EndTime:hh\\:mm}";
            if (e.FacultyId == cand.FacultyId)
                return $"{(e.FacultyName ?? "This teacher").Trim()} already has {e.SubjectName.Trim()} ({e.GroupLabel}) on {when}.";
            if (SameStudents(cand, e))
                return $"{e.GroupLabel} already has {e.SubjectName.Trim()} with {(e.FacultyName ?? "another teacher").Trim()} on {when}.";
        }
        return null;
    }

    /// <summary>Counts per teacher / class group / subject. groupBy = "teacher" | "class" | "subject".</summary>
    public static List<LectureSummaryRow> Summarize(IEnumerable<Lecture> lectures, string groupBy, DateTime now)
    {
        string kind = (groupBy ?? "teacher").ToLowerInvariant();
        var rows = lectures.GroupBy(l => kind switch
        {
            "class"   => $"{l.ClassId}|{l.SectionId}|{l.BatchId}",
            "subject" => l.SubjectName.Trim().ToLowerInvariant(),
            _         => l.FacultyId.ToString(),
        }).Select(g =>
        {
            var first = g.First();
            return new LectureSummaryRow
            {
                Key       = g.Key,
                Label     = kind switch { "class" => first.GroupLabel, "subject" => first.SubjectName.Trim(), _ => (first.FacultyName ?? "—").Trim() },
                FacultyId = kind == "teacher" ? first.FacultyId : null,
                Scheduled = g.Count(l => l.Status == LectureStatus.Scheduled),
                Completed = g.Count(l => l.Status == LectureStatus.Completed),
                Cancelled = g.Count(l => l.Status == LectureStatus.Cancelled),
                HoursHeld = Math.Round(g.Where(l => l.Status == LectureStatus.Completed).Sum(l => l.DurationHours), 2),
                NotUpdated = g.Count(l => l.Status == LectureStatus.Scheduled && IsPast(l, now)),
            };
        });
        return rows.OrderByDescending(r => r.Completed).ThenByDescending(r => r.Total).ThenBy(r => r.Label).ToList();
    }

    public static bool IsPast(Lecture l, DateTime now)
        => l.LectureDate.Date < now.Date || (l.LectureDate.Date == now.Date && l.EndTime < now.TimeOfDay);

    // ── Operations ──────────────────────────────────────────────────────────────────────────

    /// <summary>Validates and saves (create, or update when LectureId > 0). Returns (error, number of lectures saved).</summary>
    public (string? error, int count) Save(LectureSaveReq r, int userId)
    {
        var subject = (r.SubjectName ?? "").Trim();
        if (r.ClassId <= 0) return ("Select a class.", 0);
        if (subject == "") return ("Select a subject.", 0);
        if (r.FacultyId <= 0) return ("Select a teacher.", 0);
        if (!TryParseTime(r.StartTime, out var start) || !TryParseTime(r.EndTime, out var end)) return ("Enter a valid start and end time.", 0);
        if (end <= start) return ("End time must be after the start time.", 0);

        bool isEdit = r.LectureId > 0;
        var (dateErr, dates) = ExpandDates(isEdit ? new LectureSaveReq { Date = r.Date } : r);
        if (dateErr != null) return (dateErr, 0);

        Lecture Make(DateTime d, Guid? series) => new()
        {
            LectureId = isEdit ? r.LectureId : 0, LectureDate = d, StartTime = start, EndTime = end,
            ClassId = r.ClassId, SectionId = r.SectionId, BatchId = r.BatchId, SubjectName = subject, FacultyId = r.FacultyId,
            Topic = string.IsNullOrWhiteSpace(r.Topic) ? null : r.Topic.Trim(),
            Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim(), SeriesId = series,
        };

        Guid? seriesId = !isEdit && r.Repeat && dates.Count > 1 ? Guid.NewGuid() : null;
        var candidates = dates.Select(d => Make(d, seriesId)).ToList();

        // Clash check against everything already scheduled in the affected period (and among the new ones themselves).
        var existing = repo.Search(new LectureFilter { From = dates.Min(), To = dates.Max() });
        foreach (var c in candidates)
        {
            var clash = FindConflict(c, existing);
            if (clash != null) return (clash, 0);
        }

        if (isEdit)
        {
            if (repo.GetById(r.LectureId) == null) return ("Lecture not found.", 0);
            repo.Update(candidates[0], userId);
            return (null, 1);
        }
        repo.InsertMany(candidates, userId);
        return (null, candidates.Count);
    }

    /// <summary>Marks Scheduled / Completed / Cancelled. A lecture that has not happened yet cannot be marked Completed.</summary>
    public string? SetStatus(int id, string status, string? note, int userId)
    {
        if (!LectureStatus.All.Contains(status)) return "Unknown status.";
        var l = repo.GetById(id);
        if (l == null) return "Lecture not found.";
        if (status == LectureStatus.Completed && l.LectureDate.Date > DateTime.Today)
            return "This lecture is in the future — it cannot be marked as completed yet.";
        if (status == LectureStatus.Scheduled)
        {
            // Re-opening must not create a clash with something scheduled in the meantime.
            var existing = repo.Search(new LectureFilter { From = l.LectureDate, To = l.LectureDate });
            var clash = FindConflict(l, existing);
            if (clash != null) return clash;
        }
        repo.SetStatus(id, status, status == LectureStatus.Cancelled ? note : null, userId);
        return null;
    }

    public bool Delete(int id, int userId) => repo.SoftDelete(id, userId) > 0;

    /// <summary>Deletes this lecture and every later, not-yet-completed lecture of the same repeating schedule.</summary>
    public int DeleteSeriesFrom(int lectureId, int userId)
    {
        var l = repo.GetById(lectureId);
        if (l == null) return 0;
        if (l.SeriesId == null) return repo.SoftDelete(lectureId, userId);
        return repo.SoftDeleteSeries(l.SeriesId.Value, l.LectureDate, userId);
    }
}
