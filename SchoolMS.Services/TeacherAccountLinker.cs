using SchoolMS.Domain;

namespace SchoolMS.Services;

/// The Users table and the Faculty table have no foreign key between them, so a teacher's login has to be
/// matched to their Faculty profile. To never show one teacher another teacher's money the rule is strict:
///   1. same phone number (last 10 digits) as exactly one active faculty member, else
///   2. same normalised full name as exactly one active faculty member, else
///   3. same first name as exactly one active faculty member.
/// Anything ambiguous or unmatched returns null ("not linked") rather than guessing.
public class TeacherAccountLinker(UserMgmtService userSvc, FacultyService facultySvc)
{
    static readonly HashSet<string> Honorifics = new(StringComparer.OrdinalIgnoreCase)
        { "sir", "maam", "madam", "mam", "ms", "mr", "mrs", "miss", "teacher" };

    public static string NormalizeName(string? name)
    {
        var cleaned = new string((name ?? "").ToLowerInvariant().Replace("'", "").Replace("\u2019", "")
            .Select(c => char.IsLetter(c) ? c : ' ').ToArray());
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => !Honorifics.Contains(t)));
    }

    static string Last10(string? phone)
    {
        var d = new string((phone ?? "").Where(char.IsDigit).ToArray());
        return d.Length >= 10 ? d[^10..] : "";
    }

    public Faculty? FindFaculty(int userId)
    {
        var user = userSvc.GetById(userId);
        if (user == null) return null;
        var active = facultySvc.GetAllActive();

        var phone = Last10(user.Phone);
        if (phone != "")
        {
            var byPhone = active.Where(f => Last10(f.Phone) == phone || Last10(f.AlternatePhone) == phone).ToList();
            if (byPhone.Count == 1) return byPhone[0];
        }

        var name = NormalizeName(user.FullName);
        if (name == "") return null;

        var exact = active.Where(f => NormalizeName(f.FullName) == name).ToList();
        if (exact.Count == 1) return exact[0];

        var first = name.Split(' ')[0];
        var byFirst = active.Where(f => NormalizeName(f.FullName).Split(' ').FirstOrDefault() == first).ToList();
        return byFirst.Count == 1 ? byFirst[0] : null;
    }
}
