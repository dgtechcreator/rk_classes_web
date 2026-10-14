namespace SchoolMS.Domain;

/// <summary>One Class Representative: a student with a staff-type login limited to his own class.</summary>
public class ClassRep
{
    public int     CrId{get;set;}
    public int     UserId{get;set;}
    public string  Username{get;set;}="";
    public bool    UserActive{get;set;}
    public int     StudentId{get;set;}
    public string  StudentName{get;set;}="";
    public string  AdmissionNo{get;set;}="";
    public string? StudentStatus{get;set;}
    public int?    ClassId{get;set;}
    public string? ClassName{get;set;}
    public int?    SectionId{get;set;}
    public string? SectionName{get;set;}
    public int?    BatchId{get;set;}
    public string? BatchName{get;set;}
    public bool    LimitToBatch{get;set;}
    public bool    IsActive{get;set;}
    public DateTime CreatedAt{get;set;}
    /// <summary>Module keys this CR holds: attendance_entry / teacher_attendance / marks_entry.</summary>
    public List<string> Rights{get;set;} = new();

    /// <summary>Working CR = CR row on, login on, student still Active.</summary>
    public bool IsWorking => IsActive && UserActive && string.Equals(StudentStatus, "Active", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// What a CR may touch. Everything a CR sees or saves is checked against this, server-side, on web and API.
/// Class is always matched; section / batch are matched only when the student has one.
/// </summary>
public class CrScope
{
    /// <summary>A CR can mark / edit today and this many previous days only.</summary>
    public const int EditDays = 2;

    /// <summary>Test marks may be entered / corrected for tests of the last 7 days (papers are checked a few days later).</summary>
    public const int MarksEditDays = 7;

    public int     CrId{get;set;}
    public int     UserId{get;set;}
    public int     StudentId{get;set;}
    public string  StudentName{get;set;}="";
    public int     ClassId{get;set;}
    public string? ClassName{get;set;}
    public int?    SectionId{get;set;}
    public string? SectionName{get;set;}
    public int?    BatchId{get;set;}
    public string? BatchName{get;set;}
    public bool    LimitToBatch{get;set;}

    /// <summary>Batch that actually restricts the CR (null = all batches of the class).</summary>
    public int? EffectiveBatchId => LimitToBatch ? BatchId : null;

    public bool Matches(int? classId, int? sectionId, int? batchId)
        => classId == ClassId
        && (SectionId == null || sectionId == SectionId)
        && (EffectiveBatchId == null || batchId == EffectiveBatchId);

    public bool Matches(Student s) => Matches(s.ClassId, s.SectionId, s.BatchId);

    public static DateTime EarliestMarksDate => DateTime.Today.AddDays(-MarksEditDays);
    public static DateTime ClampMarksDate(DateTime d) => d.Date < EarliestMarksDate ? EarliestMarksDate : (d.Date > DateTime.Today ? DateTime.Today : d.Date);

    /// <summary>Oldest date a CR may still mark / edit.</summary>
    public static DateTime EarliestDate => DateTime.Today.AddDays(-EditDays);
    public static DateTime Clamp(DateTime d) => d.Date < EarliestDate ? EarliestDate : (d.Date > DateTime.Today ? DateTime.Today : d.Date);
}
