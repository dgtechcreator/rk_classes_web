namespace SchoolMS.Domain;

/// <summary>
/// Builds the display name used everywhere a student is listed: <c>Surname StudentName FatherName MotherName</c>.
///
/// The Students table has no separate surname column. <c>FullName</c> is entered as "StudentName FatherName Surname"
/// (e.g. "Aditya Jayprakash Yadav"), while FatherName / MotherName may or may not repeat the surname, so the pieces
/// are derived rather than stored. Pure function — the stored data is never changed.
/// </summary>
public static class StudentNameFormatter
{
    public static string Format(string? fullName, string? fatherName, string? motherName)
    {
        var name = Words(fullName);
        if (name.Count == 0) return "";
        var father = Words(fatherName);
        var mother = Words(motherName);

        // Surname = last word of the student's own name, unless that word is really the father's name
        // ("SINGH SANDHYA DILIP" / "SOHAIL SHAIKH SAMIULLAH" keep the father last) — then use the father's
        // surname, or the word before it. A single word has no surname to move.
        int si = name.Count >= 2 ? name.Count - 1 : -1;
        if (si >= 0 && father.Count > 0 && Similar(name[si], father[0]))
            si = father.Count >= 2 ? name.FindLastIndex(w => Similar(w, father[^1])) : name.Count >= 3 ? name.Count - 2 : -1;
        string? surname = si >= 0 ? name[si] : null;
        var own = new List<string>(name);
        if (si >= 0) own.RemoveAt(si);
        else if (name.Count == 1 && father.Count >= 2) surname = father[^1];

        var fatherGiven = WithoutSurname(father, surname);
        var motherGiven = WithoutSurname(mother, surname);

        // FullName usually already carries the father's name after the first word — only add it when it is missing.
        bool fatherInName = own.Skip(1).Any(w => fatherGiven.Any(f => Similar(w, f)));
        var parts = new List<string>();
        if (surname != null) parts.Add(surname);
        parts.AddRange(own);
        if (!fatherInName) parts.AddRange(fatherGiven);

        // A mother's name is often written "Gyandevi Rameshchandra Yadav" (with her husband's name) — keep her own name only.
        if (fatherGiven.Count > 0)
        {
            var trimmed = motherGiven.Where((w, i) => i == 0 || !Similar(w, fatherGiven[0])).ToList();
            if (trimmed.Count > 0) motherGiven = trimmed;
        }
        parts.AddRange(motherGiven);

        return string.Join(" ", parts);
    }

    static List<string> Words(string? s) =>
        (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim(' ', '.', ',')).Where(w => w.Length > 0).ToList();

    // "Kamlesh dubey" with surname "Dubey" -> ["Kamlesh"]; a lone word is already a given name and is kept.
    static List<string> WithoutSurname(List<string> words, string? surname)
    {
        if (surname == null || words.Count < 2 || !Similar(words[^1], surname)) return new List<string>(words);
        return words.Take(words.Count - 1).ToList();
    }

    // Case-insensitive match that forgives spelling slips (Jayprakash / Jaiprakash, Virender / Virendra) and a
    // shortened form (Afzal / Afzalhussain).
    static bool Similar(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        a = a.ToLowerInvariant(); b = b.ToLowerInvariant();
        int min = Math.Min(a.Length, b.Length), max = Math.Max(a.Length, b.Length);
        if (min >= 4 && (a.StartsWith(b) || b.StartsWith(a))) return true;
        return max >= 5 && max - min <= 1 && Distance(a, b) <= Math.Max(1, max / 4);
    }

    static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}
