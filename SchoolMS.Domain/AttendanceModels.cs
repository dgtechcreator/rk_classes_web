namespace SchoolMS.Domain;

public class AttendanceRecord
{
    public int    StudentId{get;set;}
    public string FullName{get;set;}="";
    public string AdmissionNo{get;set;}="";
    public string? RollNo{get;set;}
    public int? ClassId{get;set;}
    public string? ClassName{get;set;}
    public int? SectionId{get;set;}
    public string? SectionName{get;set;}
    public int? BatchId{get;set;}
    public string? BatchName{get;set;}
    public string? ProfilePicPath{get;set;}
    public string   AttendanceStatus{get;set;}="Present";
    public int?     AttendanceId{get;set;}
    public string?  Remarks{get;set;}
    public string?  Subject{get;set;}
    public string?  SirName{get;set;}
    public TimeSpan? StartTime{get;set;}
    public TimeSpan? EndTime{get;set;}
    public string?  Phone{get;set;}
    public string?  FatherPhone{get;set;}
    public string?  MotherPhone{get;set;}
}

public class AttendanceReport
{
    public int     StudentId{get;set;}
    public string  FullName{get;set;}="";
    public string  AdmissionNo{get;set;}="";
    public string? ClassName{get;set;}
    public int     PresentDays{get;set;}
    public int     AbsentDays{get;set;}
    public int     LateDays{get;set;}
    public int     TotalDays{get;set;}
    public decimal AttendancePct{get;set;}
    public int?    CreatedBy{get;set;}
    public string? CreatedByName{get;set;}
}

public class StudentAttendanceDetail
{
    public DateTime AttendanceDate { get; set; }
    public string   Status         { get; set; } = "Present";
    public string?  Subject        { get; set; }
    public string?  SirName        { get; set; }
    public string?  Remarks        { get; set; }
}

public class DateAttendanceEntry
{
    public int      StudentId{get;set;}
    public DateTime AttendanceDate{get;set;}
    public string   Status{get;set;}="Present";
}

public class AttendanceBatch
{
    public int BatchId { get; set; }
    public string BatchName { get; set; } = "";
    public List<int> StudentIds { get; set; } = new();
    public int StudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One attendance batch's status for a single date — drives the green/red batch cards.</summary>
public class AttendanceBatchSummary
{
    public int    BatchId      { get; set; }
    public string BatchName    { get; set; } = "";
    /// <summary>Active students currently in the batch.</summary>
    public int    Total        { get; set; }
    public int    Present      { get; set; }
    public int    Absent       { get; set; }
    public int    Late         { get; set; }
    /// <summary>Students that already have an attendance row for the date.</summary>
    public int    Marked       { get; set; }
    public int    Pending      => Total - Marked;
    /// <summary>True once attendance was taken for at least one student of the batch on the date.</summary>
    public bool   IsMarked     => Marked > 0;
}
