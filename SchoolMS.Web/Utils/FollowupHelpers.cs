using System.Text;
using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Utils;

/// <summary>JSON shapes and the Excel (CSV) export of the fee follow-up module, shared by the web pages and the mobile API.</summary>
public static class FollowupHelpers
{
    static string D(DateTime? d) => d?.ToString("yyyy-MM-dd") ?? "";

    public static object Entry(FeeFollowup f) => new
    {
        followupId = f.FollowupId, studentId = f.StudentId, at = f.FollowupAt.ToString("yyyy-MM-ddTHH:mm:ss"),
        followedBy = f.FollowedBy, followedByName = f.FollowedByName, mode = f.Mode, spokeTo = f.SpokeTo, outcome = f.Outcome,
        reason = f.Reason, notes = f.Notes, promisedAmount = f.PromisedAmount, nextDate = D(f.NextDate), nextDateBy = f.NextDateBy,
        balanceAtTime = f.BalanceAtTime,
    };

    public static object Row(FollowupSummaryRow r) => new
    {
        studentId = r.StudentId, studentName = r.StudentName, admissionNo = r.AdmissionNo, className = r.ClassName, sectionName = r.SectionName,
        batchName = r.BatchName, phone = r.Phone, fatherPhone = r.FatherPhone, motherPhone = r.MotherPhone,
        balance = r.Balance, count = r.Count, status = r.Status, last = r.Last == null ? null : Entry(r.Last),
    };

    public static object SummaryPayload(List<FollowupSummaryRow> rows) => new
    {
        total = rows.Count,
        overdue = rows.Count(r => r.Status == "Overdue"), today = rows.Count(r => r.Status == "Today"), upcoming = rows.Count(r => r.Status == "Upcoming"),
        noNextDate = rows.Count(r => r.Status == "NoNextDate"), notFollowed = rows.Count(r => r.Status == "NotFollowed"),
        totalBalance = rows.Sum(r => Math.Max(0, r.Balance)),
        rows = rows.Select(Row),
    };

    public static string StatusLabel(string s) => s switch
    {
        "Overdue" => "Overdue", "Today" => "Due today", "Upcoming" => "Upcoming", "NoNextDate" => "No next date",
        "NotFollowed" => "Not followed up", "PaidUp" => "Paid up", _ => s,
    };

    static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    static string N(decimal? v) => v.HasValue ? v.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "";
    static string Dt(DateTime? d) => d?.ToString("dd-MM-yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    static string Dd(DateTime? d) => d?.ToString("dd-MM-yyyy") ?? "";

    /// <summary>A file Excel opens (CSV, UTF-8 with BOM): the summary (one line per student), then every follow-up in detail.</summary>
    public static byte[] Csv(List<FollowupSummaryRow> summary, List<FeeFollowup> details)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FEE FOLLOW-UP SUMMARY");
        sb.AppendLine("Student,Admission No,Class,Stream / Medium,Batch,Phone,Father Phone,Mother Phone,Balance,Follow-ups,Status,Last follow-up,Followed by,Mode,Spoke to,Outcome,Reason,What was said,Promised amount,Next date,Next date given by");
        foreach (var r in summary)
        {
            var l = r.Last;
            sb.AppendLine(string.Join(",", Q(r.StudentName), Q(r.AdmissionNo), Q(r.ClassName), Q(r.SectionName), Q(r.BatchName), Q(r.Phone), Q(r.FatherPhone), Q(r.MotherPhone),
                N(r.Balance), r.Count, Q(StatusLabel(r.Status)), Q(Dt(l?.FollowupAt)), Q(l?.FollowedByName), Q(l?.Mode), Q(l?.SpokeTo), Q(l?.Outcome), Q(l?.Reason), Q(l?.Notes),
                N(l?.PromisedAmount), Q(Dd(l?.NextDate)), Q(l == null || l.NextDate == null ? "" : l.NextDateBy == "Parent" ? "Parent gave" : "Set by us")));
        }
        sb.AppendLine();
        sb.AppendLine("ALL FOLLOW-UPS (DETAIL)");
        sb.AppendLine("Date & time,Student,Admission No,Class,Stream / Medium,Batch,Followed by,Mode,Spoke to,Outcome,Reason,What was said,Promised amount,Balance then,Next date,Next date given by");
        foreach (var f in details.OrderBy(x => (x.StudentName ?? "")).ThenByDescending(x => x.FollowupAt))
            sb.AppendLine(string.Join(",", Q(Dt(f.FollowupAt)), Q(f.StudentName), Q(f.AdmissionNo), Q(f.ClassName), Q(f.SectionName), Q(f.BatchName), Q(f.FollowedByName), Q(f.Mode), Q(f.SpokeTo),
                Q(f.Outcome), Q(f.Reason), Q(f.Notes), N(f.PromisedAmount), N(f.BalanceAtTime), Q(Dd(f.NextDate)), Q(f.NextDate == null ? "" : f.NextDateBy == "Parent" ? "Parent gave" : "Set by us")));
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }
}
