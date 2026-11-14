namespace SchoolMS.Domain;

/// <summary>One fee follow-up: somebody from the institute spoke to (or visited) a student's family about pending fees.</summary>
public class FeeFollowup
{
    public int       FollowupId   { get; set; }
    public int       StudentId    { get; set; }
    /// <summary>When the follow-up was done (date + time).</summary>
    public DateTime  FollowupAt   { get; set; }
    public int?      FollowedBy   { get; set; }
    public string    FollowedByName { get; set; } = "";
    /// <summary>Call, WhatsApp, Visit, SMS, Other.</summary>
    public string    Mode         { get; set; } = "Call";
    /// <summary>Father, Mother, Student, Other.</summary>
    public string?   SpokeTo      { get; set; }
    /// <summary>What came of it - see <see cref="FeeFollowupRules.Outcomes"/>.</summary>
    public string    Outcome      { get; set; } = "";
    /// <summary>Why the fees are pending (as told).</summary>
    public string?   Reason       { get; set; }
    /// <summary>What was said - the conversation.</summary>
    public string?   Notes        { get; set; }
    public decimal?  PromisedAmount { get; set; }
    /// <summary>When to follow up (or when the parent will pay) next.</summary>
    public DateTime? NextDate     { get; set; }
    /// <summary>"Parent" = the parent gave this date; "Self" = the follow-up person set it as a reminder to call again.</summary>
    public string?   NextDateBy   { get; set; }
    /// <summary>The balance of the student at that moment.</summary>
    public decimal?  BalanceAtTime { get; set; }
    public DateTime  CreatedAt    { get; set; }

    // joined for lists / export
    public string?   StudentName  { get; set; }
    public string?   AdmissionNo  { get; set; }
    public string?   ClassName    { get; set; }
    public string?   SectionName  { get; set; }
    public string?   BatchName    { get; set; }
}

public static class FeeFollowupRules
{
    public static readonly string[] Modes    = { "Call", "WhatsApp", "Visit", "SMS", "Other" };
    public static readonly string[] SpokeTo  = { "Father", "Mother", "Student", "Other" };
    public static readonly string[] Outcomes =
    {
        "Promised to pay", "Will pay part now", "Asked for more time", "Already paid",
        "No answer", "Phone switched off", "Wrong number", "Refused to pay", "Other",
    };
}

/// <summary>One line of the follow-up summary: a student, the balance, the latest follow-up and what is due next.</summary>
public class FollowupSummaryRow
{
    public int      StudentId   { get; set; }
    public string   StudentName { get; set; } = "";
    public string   AdmissionNo { get; set; } = "";
    public string   ClassName   { get; set; } = "";
    public string   SectionName { get; set; } = "";
    public string   BatchName   { get; set; } = "";
    public string   Phone       { get; set; } = "";
    public string   FatherPhone { get; set; } = "";
    public string   MotherPhone { get; set; } = "";
    public decimal  Balance     { get; set; }
    public int      Count       { get; set; }
    public FeeFollowup? Last    { get; set; }
    /// <summary>NotFollowed | NoNextDate | Overdue | Today | Upcoming | PaidUp</summary>
    public string   Status      { get; set; } = "NotFollowed";
}
