namespace SchoolMS.Domain;

/// <summary>
/// One study follow-up of a student: either a call to the parents about the child's studies ("Call": what the parent said,
/// what was told) or an oral test ("Oral": subject, topic, result, optional score).
/// </summary>
public class StudyFollowup
{
    public int       FollowupId   { get; set; }
    public int       StudentId    { get; set; }
    /// <summary>When it was done (date + time).</summary>
    public DateTime  FollowupAt   { get; set; }
    public int?      FollowedBy   { get; set; }
    public string    FollowedByName { get; set; } = "";
    /// <summary>"Call" or "Oral" - see <see cref="StudyRules.Kinds"/>.</summary>
    public string    Kind         { get; set; } = StudyRules.Call;

    // ── Kind = Call ──
    public string?   Mode         { get; set; }
    public string?   SpokeTo      { get; set; }
    /// <summary>What the call was about (studies, test marks, homework...).</summary>
    public string?   Category     { get; set; }
    public string?   Outcome      { get; set; }

    // ── Kind = Oral ──
    public string?   Subject      { get; set; }
    public string?   Topic        { get; set; }
    /// <summary>See <see cref="StudyRules.Results"/>.</summary>
    public string?   Result       { get; set; }
    public decimal?  Marks        { get; set; }
    public decimal?  OutOf        { get; set; }

    /// <summary>What the parent said.</summary>
    public string?   ParentSaid   { get; set; }
    /// <summary>What we said / the conversation (call) or the teacher's remark (oral test).</summary>
    public string?   Notes        { get; set; }
    /// <summary>When to follow up again.</summary>
    public DateTime? NextDate     { get; set; }

    /// <summary>The parent was told through a notification (on save).</summary>
    public bool      NotifyParent { get; set; }
    public bool      ParentNotified { get; set; }
    public DateTime? NotifiedAt   { get; set; }
    public DateTime  CreatedAt    { get; set; }

    // joined for lists / export
    public string?   StudentName  { get; set; }
    public string?   AdmissionNo  { get; set; }
    public string?   ClassName    { get; set; }
    public string?   SectionName  { get; set; }
    public string?   BatchName    { get; set; }

    public bool IsOral => Kind == StudyRules.Oral;
}

public static class StudyRules
{
    public const string Call = "Call";
    public const string Oral = "Oral";

    public static readonly string[] Kinds      = { Call, Oral };
    public static readonly string[] Modes      = { "Call", "WhatsApp", "Visit", "SMS", "Other" };
    public static readonly string[] SpokeTo    = { "Father", "Mother", "Student", "Other" };
    public static readonly string[] Categories = { "Studies in general", "Test marks", "Homework / practice", "Attendance / regularity", "Behaviour / attention", "Oral test", "Other" };
    public static readonly string[] Outcomes   =
    {
        "Parent will take care", "Needs more attention", "Improving", "Parent satisfied",
        "Parent has a complaint", "No answer", "Phone switched off", "Wrong number", "Other",
    };
    public const string Gave = "Gave it", Partly = "Partly", CouldNot = "Could not give", NotTaken = "Not taken";
    public static readonly string[] Results = { Gave, Partly, CouldNot, NotTaken };

    public static string KindLabel(string kind) => kind == Oral ? "Oral test" : "Parent call";
}

/// <summary>Everything about the students that have at least one study follow-up (in the chosen period).</summary>
public class StudyStudentRow
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
    public int      Calls       { get; set; }
    public int      Orals       { get; set; }
    public int      Gave        { get; set; }
    public int      Partly      { get; set; }
    public int      CouldNot    { get; set; }
    public StudyFollowup? Last  { get; set; }
    /// <summary>The follow-up date set by the latest entry (a later entry without a date clears it).</summary>
    public DateTime? NextDate   { get; set; }
    public bool     Due         => NextDate != null && NextDate.Value.Date <= DateTime.Today;
}

public class StudySummary
{
    public List<StudyFollowup>  Entries  { get; set; } = new();
    public List<StudyStudentRow> Students { get; set; } = new();
    public int Calls => Entries.Count(e => !e.IsOral);
    public int Orals => Entries.Count(e => e.IsOral);
    public int Gave => Entries.Count(e => e.IsOral && e.Result == StudyRules.Gave);
    public int Partly => Entries.Count(e => e.IsOral && e.Result == StudyRules.Partly);
    public int CouldNot => Entries.Count(e => e.IsOral && e.Result == StudyRules.CouldNot);
    public int DueStudents => Students.Count(s => s.Due);
}

/// <summary>The student's own details for the follow-up page.</summary>
public class StudyStudentCard
{
    public int    StudentId   { get; set; }
    public string FullName    { get; set; } = "";
    public string AdmissionNo { get; set; } = "";
    public string ClassName   { get; set; } = "";
    public string SectionName { get; set; } = "";
    public string BatchName   { get; set; } = "";
    public string Phone       { get; set; } = "";
    public string FatherPhone { get; set; } = "";
    public string MotherPhone { get; set; } = "";
    public string Group => string.Join(" · ", new[] { ClassName, SectionName, BatchName }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
