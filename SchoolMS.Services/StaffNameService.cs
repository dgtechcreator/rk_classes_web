using SchoolMS.Domain;

namespace SchoolMS.Services;

/// <summary>
/// How a staff member is greeted everywhere: Teachers and Admins appear as "FirstName Sir" / "FirstName Ma'am".
/// Gender comes from the linked Faculty profile, else from the user's own Gender; if it is unknown the full name is
/// shown unchanged (never a guessed honorific). Other roles (Accountant, Class Representative ...) keep their full name.
/// </summary>
public class StaffNameService(TeacherAccountLinker linker, UserMgmtService users)
{
    static readonly HashSet<string> Honorifics = new(StringComparer.OrdinalIgnoreCase)
        { "sir", "maam", "ma'am", "madam", "mam", "mr", "mr.", "mrs", "mrs.", "ms", "ms.", "miss", "dr", "dr." };

    /// <summary>Greeting name of one login ("" when the user does not exist).</summary>
    public string DisplayName(int userId)
    {
        var u = users.GetById(userId);
        if (u == null) return "";
        return For(u, () => linker.FindFaculty(userId)?.Gender);
    }

    public static bool IsTeacherOrAdmin(int roleId, string? roleName)
        => roleId == 1 || (roleName ?? "").Contains("teacher", StringComparison.OrdinalIgnoreCase);

    /// <param name="facultyGender">Lazy: only looked up for Teacher / Admin logins.</param>
    public static string For(AppUser u, Func<string?> facultyGender)
    {
        if (!IsTeacherOrAdmin(u.RoleId, u.RoleName)) return u.FullName;
        return Format(u.FullName, Normalize(facultyGender()) ?? Normalize(u.Gender));
    }

    /// <summary>"male" / "female" or null when unknown / other.</summary>
    static string? Normalize(string? gender)
    {
        var g = (gender ?? "").Trim().ToLowerInvariant();
        if (g is "male" or "m" or "boy") return "male";
        if (g is "female" or "f" or "girl") return "female";
        return null;
    }

    public static string Format(string fullName, string? normalizedGender)
    {
        if (normalizedGender == null) return fullName;
        var first = FirstName(fullName);
        if (first == "") return fullName;
        return first + (normalizedGender == "female" ? " Ma'am" : " Sir");
    }

    /// <summary>First real name token, skipping a leading honorific ("Dr. Kapil Yadav" -> "Kapil") and a trailing one ("Kapil Sir" -> "Kapil").</summary>
    public static string FirstName(string fullName)
    {
        var parts = (fullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Honorifics.Contains(p.Trim().TrimEnd('.', ',')) && !Honorifics.Contains(p)).ToList();
        if (parts.Count == 0) return "";
        var first = parts[0];
        // "KAPIL" -> "Kapil" (names typed in capitals); mixed case is left alone
        return first.Length > 1 && first == first.ToUpperInvariant() ? char.ToUpperInvariant(first[0]) + first[1..].ToLowerInvariant() : first;
    }
}
