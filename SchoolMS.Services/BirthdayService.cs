using System.Collections.Concurrent;
using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public record BirthdayCard(int StudentId, string Name, string Title, string Message);

/// <summary>One person whose birthday is today, for the staff dashboard (a student, or a faculty member).</summary>
public record StaffBirthday(string Kind, int Id, string Name, string Line, int Age, string FatherPhone, string MotherPhone, string Phone, string Message);

/// <summary>
/// "Happy Birthday" for the Student / Parent login: on a child's birthday the website and the app show a card
/// ("Happy Birthday Vivek - from RK Classes" + the wish) and the wish also lands in Notifications once.
/// The wish text is the active "Birthday" message template (Masters -> Templates), so the admin edits it there.
/// </summary>
public class BirthdayService(ParentService parents, MastersService masters, ActivityNotifier notifier, StudentService students, FacultyService faculty)
{
    public const string Category = "Birthday";
    public const string DefaultMessage = "Wishing you a very Happy Birthday! \U0001F382 May this year bring you joy, good health and great success. Keep shining and keep learning! \U0001F31F";

    static readonly ConcurrentDictionary<string, bool> Notified = new();

    /// <summary>A 29 Feb birthday is celebrated on 28 Feb in years that have no 29 Feb.</summary>
    public static bool IsBirthday(DateTime? dob, DateTime today)
    {
        if (dob == null) return false;
        var d = dob.Value;
        if (d.Month == today.Month && d.Day == today.Day) return true;
        return d.Month == 2 && d.Day == 29 && !DateTime.IsLeapYear(today.Year) && today.Month == 2 && today.Day == 28;
    }

    string Template()
    {
        try
        {
            var t = masters.GetMessageTemplates().FirstOrDefault(x => string.Equals(x.Category, Category, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Body));
            if (t != null) return t.Body;
        }
        catch { /* template table missing: the default wish is used */ }
        return DefaultMessage;
    }

    string Fill(string body, string name, string cls, string medium, DateTime today)
        => System.Text.RegularExpressions.Regex.Replace(body, @"\{(\w+)\}", m => m.Groups[1].Value.ToLowerInvariant() switch {
            "student" => name, "class" => cls, "medium" => medium, "date" => today.ToString("dd-MM-yyyy"), "school" => "RK Classes", _ => m.Value });

    /// <summary>Everyone whose birthday is today: active students of the current year and active faculty. Students first, then faculty. Never throws.</summary>
    public List<StaffBirthday> TodayForStaff()
    {
        var list = new List<StaffBirthday>();
        var today = DateTime.Today;
        var body = Template();
        try
        {
            var (kids, _) = students.GetAll(1, 100000, null, null, null, null, null, "Active");
            foreach (var s in kids.Where(s => IsBirthday(s.DateOfBirth, today)).OrderBy(s => s.ClassName).ThenBy(s => s.FullName))
            {
                var name = ActivityRepo.ShortName(s.FullName, s.FatherName, s.MotherName);
                var line = string.Join(" · ", new[] { s.ClassName, s.SectionName, s.BatchName }.Select(x => (x ?? "").Trim()).Where(x => x != ""));
                list.Add(new StaffBirthday("Student", s.StudentId, name, line, today.Year - s.DateOfBirth!.Value.Year,
                    s.FatherPhone ?? "", s.MotherPhone ?? "", s.Phone ?? "", Fill(body, name, (s.ClassName ?? "").Trim(), (s.SectionName ?? "").Trim(), today)));
            }
        }
        catch { /* students are optional here */ }
        try
        {
            var (staff, _) = faculty.GetAll(null, "Active", null, 1, 10000);
            foreach (var f in staff.Where(f => IsBirthday(f.DateOfBirth, today)).OrderBy(f => f.FullName))
                list.Add(new StaffBirthday("Faculty", f.FacultyId, f.FullName, string.IsNullOrWhiteSpace(f.DesignationName) ? "Faculty" : f.DesignationName!.Trim(),
                    today.Year - f.DateOfBirth!.Value.Year, "", "", f.Phone ?? "", Fill(body, f.FullName, "", "", today)));
        }
        catch { /* faculty are optional here */ }
        return list;
    }

    /// <summary>Birthday cards of the children linked to this parent / student login (empty on all other days). Never throws.</summary>
    public List<BirthdayCard> ForParent(string phone)
    {
        var cards = new List<BirthdayCard>();
        try
        {
            var today = DateTime.Today;
            var kids = parents.GetChildren(phone).Where(c => IsBirthday(c.DateOfBirth, today)).ToList();
            if (kids.Count == 0) return cards;
            var body = Template();
            foreach (var c in kids)
            {
                var name = ActivityRepo.ShortName(c.FullName, c.FatherName, c.MotherName);
                var msg = System.Text.RegularExpressions.Regex.Replace(body, @"\{(\w+)\}", m => m.Groups[1].Value.ToLowerInvariant() switch {
                    "student" => name, "class" => (c.ClassName ?? "").Trim(), "medium" => (c.SectionName ?? "").Trim(),
                    "date" => today.ToString("dd-MM-yyyy"), "school" => "RK Classes", _ => m.Value });
                var title = $"Happy Birthday {name}! \U0001F382";
                cards.Add(new BirthdayCard(c.StudentId, name, title, msg));
                if (Notified.TryAdd($"{c.StudentId}:{today:yyyyMMdd}:{msg.GetHashCode()}", true))
                    notifier.BirthdayWish(c.StudentId, today.Year, title, msg);
            }
        }
        catch { /* a birthday card must never break a page */ }
        return cards;
    }
}
