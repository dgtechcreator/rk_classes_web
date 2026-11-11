namespace SchoolMS.Domain;

/// <summary>
/// One student who stopped coming to the classes (left on their own, or removed by the institute). The money figures are a SNAPSHOT
/// of the day it was recorded, so the summary always shows what had been paid and what was pending at that moment.
/// </summary>
public class DiscontinuedStudent
{
    public int      Id { get; set; }
    public int      StudentId { get; set; }
    public string   StudentName { get; set; } = "";
    public string   AdmissionNo { get; set; } = "";
    public string   ClassName { get; set; } = "";
    public string   SectionName { get; set; } = "";
    public string   BatchName { get; set; } = "";
    public string   FatherPhone { get; set; } = "";
    public string   Kind { get; set; } = DiscontinueKind.Left;
    public string   Reason { get; set; } = "";
    public DateTime LeftOn { get; set; } = DateTime.Today;
    public decimal  TotalFees { get; set; }        // fee + additional charges - discount (what was to be received)
    public decimal  Discount { get; set; }
    public decimal  Paid { get; set; }
    public decimal  Pending { get; set; }
    public decimal  AttendancePct { get; set; }
    public decimal  MarksPct { get; set; }
    public int      RecordedBy { get; set; }
    public string   RecordedByName { get; set; } = "";
    public DateTime RecordedAt { get; set; }
    /// <summary>The student's status today (Active again = they were taken back).</summary>
    public string   CurrentStatus { get; set; } = "Inactive";
    /// <summary>Set when the entry was reverted from the Discontinued page (the history stays).</summary>
    public DateTime? RevertedAt { get; set; }
    public string   RevertedByName { get; set; } = "";
    public string   RevertNote { get; set; } = "";
    /// <summary>Taken back - by Revert, or because the student is Active again for any other reason.</summary>
    public bool     Returned => RevertedAt != null || string.Equals(CurrentStatus, "Active", StringComparison.OrdinalIgnoreCase);
    /// <summary>Only the newest entry of a student that is still discontinued can be reverted.</summary>
    public bool     CanRevert { get; set; }
}

public static class DiscontinueKind
{
    public const string Left = "Left";          // the student / parents decided to stop
    public const string Removed = "Removed";    // the institute removed the student
    public static bool IsValid(string? k) => k is Left or Removed;
    public static string Label(string? k) => k == Removed ? "Removed by institute" : "Left by student / parents";
}
