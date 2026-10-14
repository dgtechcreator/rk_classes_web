namespace SchoolMS.Domain;

/// <summary>
/// One soft-deleted record of the "other" kinds (expense, teacher payment, teacher attendance entry, lecture) as listed on
/// Deleted Records. Students, faculty, users and fee receipts have their own richer lists there.
/// </summary>
public class DeletedItem
{
    /// <summary>expense | teacherpayment | teacherattendance | lecture - also the key used to restore it.</summary>
    public string Kind { get; set; } = "";
    public string KindLabel { get; set; } = "";
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByName { get; set; }
}
