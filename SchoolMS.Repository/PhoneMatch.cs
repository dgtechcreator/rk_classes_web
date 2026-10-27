namespace SchoolMS.Repository;

/// <summary>
/// Phones are typed by hand ("+91 81082 94501", "82684 61887", a trailing tab), so "same number" is decided on the LAST 10 DIGITS,
/// never on the raw text. Used wherever a parent's / student's phone is matched to a student record.
/// </summary>
public static class PhoneMatch
{
    /// <summary>"+91 81082 94501" -> "8108294501". Fewer than 7 digits = no usable number ("").</summary>
    public static string Key(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length < 7) return "";
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    /// <summary>The same rule as [Key] as a SQL expression on a column (spaces, +, -, dots, tabs and non-breaking spaces removed).</summary>
    public static string Sql(string column)
        => $"RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL({column}, ''), ' ', ''), '+', ''), '-', ''), '.', ''), CHAR(9), ''), NCHAR(160), ''), 10)";

    /// <summary>WHERE-fragment: the student's father / mother / own phone equals the parameter (@name holds a [Key]).</summary>
    public static string AnyPhoneOf(string student, string param)
        => $"({Sql(student + ".FatherPhone")} = {param} OR {Sql(student + ".MotherPhone")} = {param} OR {Sql(student + ".Phone")} = {param})";
}
