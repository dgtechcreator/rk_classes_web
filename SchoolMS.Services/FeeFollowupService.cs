using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// Fee follow-up: who followed up with whom, when, what was said, why the fees are pending and the next date (given by the
/// parent, or set by the follow-up person as a reminder to call again). The summary joins the follow-ups with the live balance.
/// </summary>
public class FeeFollowupService(FeeFollowupRepo repo, FeesService fees)
{
    public class SummaryFilter
    {
        public string? Q { get; set; }
        /// <summary>NotFollowed | NoNextDate | Overdue | Today | Upcoming | PaidUp (empty = every student with a balance or a follow-up)</summary>
        public string? Status { get; set; }
        public string? ClassName { get; set; }
        public string? SectionName { get; set; }
        public string? BatchName { get; set; }
        public int?    FollowedBy { get; set; }
        public string? Outcome { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public static string StatusOf(decimal balance, FeeFollowup? last, DateTime today)
    {
        if (last == null) return balance > 0 ? "NotFollowed" : "PaidUp";
        if (balance <= 0) return "PaidUp";
        if (last.NextDate == null) return "NoNextDate";
        var d = last.NextDate.Value.Date;
        return d < today ? "Overdue" : d == today ? "Today" : "Upcoming";
    }

    public List<FollowupSummaryRow> Summary(SummaryFilter f)
    {
        var today = DateTime.Today;
        var all = repo.Query(null, null, null, null, null);
        var byStudent = all.GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.FollowupAt).ThenByDescending(x => x.FollowupId).ToList());
        var fin = fees.GetFinanceStudents(null, null, null, true).GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());

        var ids = new HashSet<int>(byStudent.Keys);
        foreach (var kv in fin) if (kv.Value.Balance > 0) ids.Add(kv.Key);

        var rows = new List<FollowupSummaryRow>();
        foreach (var id in ids)
        {
            byStudent.TryGetValue(id, out var list);
            // a date range narrows which follow-ups count (the latest one inside the range is the one shown)
            var inRange = list?.Where(x => (f.From == null || x.FollowupAt.Date >= f.From.Value.Date) && (f.To == null || x.FollowupAt.Date <= f.To.Value.Date)).ToList();
            if ((f.From != null || f.To != null) && (inRange == null || inRange.Count == 0)) continue;
            var last = inRange?.FirstOrDefault();
            fin.TryGetValue(id, out var fr);
            var any = last ?? list?.FirstOrDefault();
            rows.Add(new FollowupSummaryRow {
                StudentId = id,
                StudentName = fr?.FullName ?? any?.StudentName ?? "",
                AdmissionNo = fr?.AdmissionNo ?? any?.AdmissionNo ?? "",
                ClassName = (fr?.ClassName ?? any?.ClassName ?? "").Trim(),
                SectionName = (fr?.SectionName ?? any?.SectionName ?? "").Trim(),
                BatchName = (fr?.BatchName ?? any?.BatchName ?? "").Trim(),
                Phone = fr?.Phone ?? "", FatherPhone = fr?.FatherPhone ?? "", MotherPhone = fr?.MotherPhone ?? "",
                Balance = fr?.Balance ?? 0,
                Count = inRange?.Count ?? list?.Count ?? 0,
                Last = last,
                Status = StatusOf(fr?.Balance ?? 0, last, today),
            });
        }

        IEnumerable<FollowupSummaryRow> q = rows;
        if (!string.IsNullOrWhiteSpace(f.ClassName))   q = q.Where(r => string.Equals(r.ClassName, f.ClassName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(f.SectionName)) q = q.Where(r => string.Equals(r.SectionName, f.SectionName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(f.BatchName))   q = q.Where(r => string.Equals(r.BatchName, f.BatchName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(f.Status))      q = q.Where(r => string.Equals(r.Status, f.Status, StringComparison.OrdinalIgnoreCase));
        if (f.FollowedBy != null)                      q = q.Where(r => r.Last?.FollowedBy == f.FollowedBy);
        if (!string.IsNullOrWhiteSpace(f.Outcome))     q = q.Where(r => r.Last?.Outcome == f.Outcome);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var words = f.Q.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            q = q.Where(r =>
            {
                var text = string.Join(' ', new[] { r.StudentName, r.AdmissionNo, r.ClassName, r.SectionName, r.BatchName, r.Phone, r.FatherPhone, r.MotherPhone, r.Last?.Notes, r.Last?.Reason, r.Last?.FollowedByName }.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();
                return words.All(text.Contains);
            });
        }
        // what needs attention first: overdue, today, upcoming (soonest first), then no next date, then never followed (biggest balance first)
        int rank(string s) => s switch { "Overdue" => 0, "Today" => 1, "Upcoming" => 2, "NoNextDate" => 3, "NotFollowed" => 4, _ => 5 };
        return q.OrderBy(r => rank(r.Status)).ThenBy(r => r.Last?.NextDate ?? DateTime.MaxValue).ThenByDescending(r => r.Balance).ThenBy(r => r.StudentName).ToList();
    }

    public List<FeeFollowup> History(int studentId) => repo.Query(studentId, null, null, null, null);

    /// <summary>Every follow-up in a period (for the Excel export's detail part).</summary>
    public List<FeeFollowup> Details(DateTime? from, DateTime? to, int? followedBy, string? outcome) => repo.Query(null, from, to, followedBy, outcome);

    public FinanceStudentRow? Finance(int studentId) => fees.GetFinanceStudents(null, null, null, true).FirstOrDefault(r => r.StudentId == studentId);

    public (string? error, int id) Add(FeeFollowup f, int by)
    {
        if (f.StudentId <= 0) return ("Choose the student.", 0);
        if (!FeeFollowupRules.Modes.Contains(f.Mode)) return ("Choose how you followed up (call, WhatsApp, visit...).", 0);
        if (string.IsNullOrWhiteSpace(f.Outcome) || !FeeFollowupRules.Outcomes.Contains(f.Outcome)) return ("Choose what came of the follow-up.", 0);
        if (!string.IsNullOrWhiteSpace(f.SpokeTo) && !FeeFollowupRules.SpokeTo.Contains(f.SpokeTo)) return ("Unknown person spoken to.", 0);
        if (f.PromisedAmount is < 0) return ("The promised amount cannot be negative.", 0);
        if (f.NextDate != null)
        {
            if (f.NextDate.Value.Date < DateTime.Today) return ("The next date cannot be in the past.", 0);
            if (f.NextDateBy != "Parent" && f.NextDateBy != "Self") f.NextDateBy = "Self";
        }
        if (string.IsNullOrWhiteSpace(f.Notes) && string.IsNullOrWhiteSpace(f.Reason))
            return ("Write what was said, or the reason the fees are pending.", 0);
        if (f.FollowupAt == default || f.FollowupAt > DateTime.Now.AddMinutes(5)) f.FollowupAt = DateTime.Now;
        f.BalanceAtTime = Finance(f.StudentId)?.Balance;
        return (null, repo.Add(f, by));
    }

    public string? Delete(int id, int userId, bool isAdmin)
    {
        var f = repo.GetById(id);
        if (f == null) return "Follow-up not found.";
        if (!isAdmin && f.FollowedBy != userId) return "You can delete only your own follow-ups.";
        repo.SoftDelete(id, userId);
        return null;
    }

    public void EnsureSchema() => repo.EnsureSchema();
}
