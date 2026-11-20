using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// Study follow-up: calls to parents about a child's studies (what the parent said, what we told them) and oral tests
/// (subject, topic, result, score). An oral test result goes straight to the parent as a notification; a call summary
/// goes only when the follow-up person asks for it. The summary lists every entry (date + time) and the students behind them.
/// </summary>
public class StudyFollowupService(StudyFollowupRepo repo, ActivityNotifier notify)
{
    public class SummaryFilter
    {
        public string? Q { get; set; }
        /// <summary>"Call" | "Oral" (empty = both)</summary>
        public string? Kind { get; set; }
        public string? ClassName { get; set; }
        public string? SectionName { get; set; }
        public string? BatchName { get; set; }
        public int?    FollowedBy { get; set; }
        public string? Subject { get; set; }
        public string? Result { get; set; }
        /// <summary>"due" = only students whose next follow-up date has come.</summary>
        public string? Status { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public StudySummary Summary(SummaryFilter f)
    {
        var all = repo.Query(null, f.From, f.To, f.FollowedBy, f.Kind);
        // the reminder of a student = the next date of its newest entry ever (not only inside the period)
        var newest = repo.Query(null, null, null, null, null).GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => g.First());

        IEnumerable<StudyFollowup> q = all;
        if (!string.IsNullOrWhiteSpace(f.ClassName))   q = q.Where(r => Same(r.ClassName, f.ClassName));
        if (!string.IsNullOrWhiteSpace(f.SectionName)) q = q.Where(r => Same(r.SectionName, f.SectionName));
        if (!string.IsNullOrWhiteSpace(f.BatchName))   q = q.Where(r => Same(r.BatchName, f.BatchName));
        if (!string.IsNullOrWhiteSpace(f.Subject))     q = q.Where(r => r.Subject != null && r.Subject.Contains(f.Subject.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(f.Result))      q = q.Where(r => r.Result == f.Result);
        if (string.Equals(f.Status, "due", StringComparison.OrdinalIgnoreCase))
            q = q.Where(r => newest.TryGetValue(r.StudentId, out var n) && n.NextDate != null && n.NextDate.Value.Date <= DateTime.Today);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var words = f.Q.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            q = q.Where(r =>
            {
                var text = string.Join(' ', new[] { r.StudentName, r.AdmissionNo, r.ClassName, r.SectionName, r.BatchName, r.Subject, r.Topic, r.Category, r.Outcome, r.Result, r.ParentSaid, r.Notes, r.FollowedByName }
                    .Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();
                return words.All(text.Contains);
            });
        }
        var entries = q.ToList();   // already newest first

        var cards = repo.GetCards(entries.Select(e => e.StudentId));
        var students = entries.GroupBy(e => e.StudentId).Select(g =>
        {
            var last = g.First();
            cards.TryGetValue(g.Key, out var c);
            newest.TryGetValue(g.Key, out var n);
            return new StudyStudentRow {
                StudentId = g.Key, StudentName = last.StudentName ?? "", AdmissionNo = last.AdmissionNo ?? "",
                ClassName = (last.ClassName ?? "").Trim(), SectionName = (last.SectionName ?? "").Trim(), BatchName = (last.BatchName ?? "").Trim(),
                Phone = c?.Phone ?? "", FatherPhone = c?.FatherPhone ?? "", MotherPhone = c?.MotherPhone ?? "",
                Calls = g.Count(x => !x.IsOral), Orals = g.Count(x => x.IsOral),
                Gave = g.Count(x => x.IsOral && x.Result == StudyRules.Gave), Partly = g.Count(x => x.IsOral && x.Result == StudyRules.Partly),
                CouldNot = g.Count(x => x.IsOral && x.Result == StudyRules.CouldNot),
                Last = last, NextDate = n?.NextDate,
            };
        })
        // students whose follow-up is due come first, then the most recently followed
        .OrderByDescending(s => s.Due).ThenByDescending(s => s.Last!.FollowupAt).ToList();

        return new StudySummary { Entries = entries, Students = students };
    }

    static bool Same(string? a, string? b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>One student's entries, newest first; [kind] / [from] / [to] narrow them.</summary>
    public List<StudyFollowup> History(int studentId, string? kind = null, DateTime? from = null, DateTime? to = null)
        => repo.Query(studentId, from, to, null, kind);

    public List<StudyFollowup> Details(DateTime? from, DateTime? to, int? followedBy, string? kind) => repo.Query(null, from, to, followedBy, kind);

    public StudyStudentCard? Card(int studentId) => repo.GetCard(studentId);

    public List<string> Subjects() => repo.GetSubjectNames();

    public (string? error, int id) Add(StudyFollowup f, int by)
    {
        if (f.StudentId <= 0 || repo.GetCard(f.StudentId) == null) return ("Choose the student.", 0);
        if (!StudyRules.Kinds.Contains(f.Kind)) return ("Choose: parent call or oral test.", 0);
        if (f.NextDate != null && f.NextDate.Value.Date < DateTime.Today) return ("The next date cannot be in the past.", 0);
        if (f.FollowupAt == default || f.FollowupAt > DateTime.Now.AddMinutes(5)) f.FollowupAt = DateTime.Now;

        if (f.Kind == StudyRules.Oral)
        {
            if (string.IsNullOrWhiteSpace(f.Subject)) return ("Write the subject of the oral test.", 0);
            if (string.IsNullOrWhiteSpace(f.Result) || !StudyRules.Results.Contains(f.Result)) return ("Choose the result: gave it, partly, could not give, or not taken.", 0);
            if (f.Marks != null)
            {
                if (f.OutOf is not > 0) return ("Write the total marks (out of) of the oral test.", 0);
                if (f.Marks < 0 || f.Marks > f.OutOf) return ("Marks must be between 0 and the total marks.", 0);
            }
            else f.OutOf = null;
            f.Mode = f.SpokeTo = f.Category = f.Outcome = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(f.Mode) || !StudyRules.Modes.Contains(f.Mode)) return ("Choose how you followed up (call, WhatsApp, visit...).", 0);
            if (!string.IsNullOrWhiteSpace(f.SpokeTo) && !StudyRules.SpokeTo.Contains(f.SpokeTo)) return ("Unknown person spoken to.", 0);
            if (!string.IsNullOrWhiteSpace(f.Category) && !StudyRules.Categories.Contains(f.Category)) return ("Unknown subject of the call.", 0);
            if (string.IsNullOrWhiteSpace(f.Outcome) || !StudyRules.Outcomes.Contains(f.Outcome)) return ("Choose how the follow-up ended.", 0);
            if (string.IsNullOrWhiteSpace(f.ParentSaid) && string.IsNullOrWhiteSpace(f.Notes)) return ("Write what the parent said, or what was discussed.", 0);
            if (f.NotifyParent && string.IsNullOrWhiteSpace(f.Notes)) return ("To tell the parent, write in \"what we told\" what should reach them.", 0);
            f.Subject = f.Topic = f.Result = null; f.Marks = f.OutOf = null;
        }

        var id = repo.Add(f, by);
        f.FollowupId = id;
        // an oral test result always goes to the parent (unless switched off); a call summary only when asked
        if (f.NotifyParent)
        {
            if (f.IsOral) notify.OralTestResult(f); else notify.StudyCallSummary(f);
            repo.MarkNotified(id);
        }
        return (null, id);
    }

    public string? Delete(int id, int userId, bool isAdmin)
    {
        var f = repo.GetById(id);
        if (f == null) return "Follow-up not found.";
        if (!isAdmin && f.FollowedBy != userId) return "You can delete only your own follow-ups.";
        repo.SoftDelete(id, userId);
        if (f.ParentNotified) notify.StudyFollowupRemoved(f.StudentId, id);
        return null;
    }

    public void EnsureSchema() => repo.EnsureSchema();
}
