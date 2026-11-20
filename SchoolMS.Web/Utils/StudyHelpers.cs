using System.Text;
using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Utils;

/// <summary>The "add follow-up" form (web and app send the same JSON). NotifyParent: empty = yes for an oral test, no for a call.</summary>
public record StudyAddReq(int StudentId, string Kind, string? Mode, string? SpokeTo, string? Category, string? Outcome, string? Subject, string? Topic,
    string? Result, decimal? Marks, decimal? OutOf, string? ParentSaid, string? Notes, string? NextDate, string? FollowupAt, bool? NotifyParent);

/// <summary>JSON shapes and the Excel (CSV) export of the study follow-up module, shared by the web pages and the mobile API.</summary>
public static class StudyHelpers
{
    public static StudyFollowup ToEntity(StudyAddReq r) => new()
    {
        StudentId = r.StudentId, Kind = r.Kind, Mode = r.Mode, SpokeTo = string.IsNullOrWhiteSpace(r.SpokeTo) ? null : r.SpokeTo,
        Category = string.IsNullOrWhiteSpace(r.Category) ? null : r.Category, Outcome = r.Outcome,
        Subject = r.Subject, Topic = r.Topic, Result = r.Result, Marks = r.Marks, OutOf = r.OutOf,
        ParentSaid = r.ParentSaid, Notes = r.Notes,
        NextDate = DateTime.TryParse(r.NextDate, out var nd) ? nd.Date : null,
        FollowupAt = DateTime.TryParse(r.FollowupAt, out var at) ? at : DateTime.Now,
        NotifyParent = r.NotifyParent ?? r.Kind == StudyRules.Oral,
    };

    /// <summary>The student search of the "add follow-up" box (same rules as the quick search of the other modules).</summary>
    public static object[] SearchStudents(StudentService students, LookupService lookup, string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Array.Empty<object>();
        var (list, _) = students.GetAll(1, 40, q.Trim(), null, null, null, lookup.GetCurrentYearId(), "Active");
        var classes = lookup.GetClasses(); var sections = lookup.GetSections();
        return list.Select(s => (object)new {
            studentId = s.StudentId, fullName = s.DisplayName, admissionNo = s.AdmissionNo,
            className = classes.FirstOrDefault(c => c.ClassId == s.ClassId)?.ClassName?.Trim() ?? "",
            sectionName = sections.FirstOrDefault(c => c.SectionId == s.SectionId)?.SectionName?.Trim() ?? "",
        }).ToArray();
    }

    static string D(DateTime? d) => d?.ToString("yyyy-MM-dd") ?? "";

    public static object Entry(StudyFollowup f) => new
    {
        followupId = f.FollowupId, studentId = f.StudentId, studentName = f.StudentName, admissionNo = f.AdmissionNo,
        className = f.ClassName?.Trim(), sectionName = f.SectionName?.Trim(), batchName = f.BatchName?.Trim(),
        at = f.FollowupAt.ToString("yyyy-MM-ddTHH:mm:ss"), followedBy = f.FollowedBy, followedByName = f.FollowedByName,
        kind = f.Kind, mode = f.Mode, spokeTo = f.SpokeTo, category = f.Category, outcome = f.Outcome,
        subject = f.Subject, topic = f.Topic, result = f.Result, marks = f.Marks, outOf = f.OutOf,
        parentSaid = f.ParentSaid, notes = f.Notes, nextDate = D(f.NextDate), parentNotified = f.ParentNotified,
    };

    public static object Row(StudyStudentRow r) => new
    {
        studentId = r.StudentId, studentName = r.StudentName, admissionNo = r.AdmissionNo, className = r.ClassName, sectionName = r.SectionName,
        batchName = r.BatchName, phone = r.Phone, fatherPhone = r.FatherPhone, motherPhone = r.MotherPhone,
        calls = r.Calls, orals = r.Orals, gave = r.Gave, partly = r.Partly, couldNot = r.CouldNot,
        nextDate = D(r.NextDate), due = r.Due, last = r.Last == null ? null : Entry(r.Last),
    };

    public static object SummaryPayload(StudySummary s) => new
    {
        total = s.Entries.Count, calls = s.Calls, orals = s.Orals, gave = s.Gave, partly = s.Partly, couldNot = s.CouldNot,
        students = s.Students.Count, dueStudents = s.DueStudents,
        entries = s.Entries.Select(Entry), studentRows = s.Students.Select(Row),
    };

    public static object StudentPayload(StudyStudentCard c, List<StudyFollowup> all, List<StudyFollowup> shown, int uid, bool isAdmin) => new
    {
        student = new {
            studentId = c.StudentId, name = c.FullName, admissionNo = c.AdmissionNo, className = c.ClassName, sectionName = c.SectionName, batchName = c.BatchName,
            phone = c.Phone, fatherPhone = c.FatherPhone, motherPhone = c.MotherPhone,
        },
        // totals are of the whole history, so the numbers do not change when the list is filtered
        totals = new {
            calls = all.Count(x => !x.IsOral), orals = all.Count(x => x.IsOral),
            gave = all.Count(x => x.IsOral && x.Result == StudyRules.Gave), partly = all.Count(x => x.IsOral && x.Result == StudyRules.Partly),
            couldNot = all.Count(x => x.IsOral && x.Result == StudyRules.CouldNot), notTaken = all.Count(x => x.IsOral && x.Result == StudyRules.NotTaken),
        },
        followups = shown.Select(r => new { entry = Entry(r), canDelete = isAdmin || r.FollowedBy == uid }),
    };

    public static object Options(List<string> subjects) => new
    {
        kinds = StudyRules.Kinds, modes = StudyRules.Modes, spokeTo = StudyRules.SpokeTo, categories = StudyRules.Categories,
        outcomes = StudyRules.Outcomes, results = StudyRules.Results, subjects,
    };

    /// <summary>"7/10" or "" - the score of an oral test.</summary>
    public static string Score(StudyFollowup f)
        => f.Marks != null && f.OutOf is > 0 ? $"{f.Marks:0.##}/{f.OutOf:0.##}" : "";

    static string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    static string Dt(DateTime? d) => d?.ToString("dd-MM-yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture) ?? "";
    static string Dd(DateTime? d) => d?.ToString("dd-MM-yyyy") ?? "";

    /// <summary>A file Excel opens (CSV, UTF-8 with BOM): every follow-up with its date and time, then the students' totals.</summary>
    public static byte[] Csv(StudySummary s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("STUDY FOLLOW-UPS (DATE & TIME WISE)");
        sb.AppendLine("Date & time,Type,Student,Admission No,Class,Stream / Medium,Batch,Done by,How,Spoke to,About,Outcome,Subject,Topic,Result,Score,What the parent said,What we told / remark,Next date,Parent notified");
        foreach (var f in s.Entries)
            sb.AppendLine(string.Join(",", Q(Dt(f.FollowupAt)), Q(StudyRules.KindLabel(f.Kind)), Q(f.StudentName), Q(f.AdmissionNo), Q(f.ClassName?.Trim()), Q(f.SectionName?.Trim()), Q(f.BatchName?.Trim()),
                Q(f.FollowedByName), Q(f.Mode), Q(f.SpokeTo), Q(f.Category), Q(f.Outcome), Q(f.Subject), Q(f.Topic), Q(f.Result), Q(Score(f)), Q(f.ParentSaid), Q(f.Notes), Q(Dd(f.NextDate)), Q(f.ParentNotified ? "Yes" : "No")));
        sb.AppendLine();
        sb.AppendLine("STUDENT-WISE SUMMARY");
        sb.AppendLine("Student,Admission No,Class,Stream / Medium,Batch,Phone,Father Phone,Mother Phone,Parent calls,Oral tests,Gave it,Partly,Could not give,Last follow-up,Next date");
        foreach (var r in s.Students)
            sb.AppendLine(string.Join(",", Q(r.StudentName), Q(r.AdmissionNo), Q(r.ClassName), Q(r.SectionName), Q(r.BatchName), Q(r.Phone), Q(r.FatherPhone), Q(r.MotherPhone),
                r.Calls, r.Orals, r.Gave, r.Partly, r.CouldNot, Q(Dt(r.Last?.FollowupAt)), Q(Dd(r.NextDate))));
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }
}
