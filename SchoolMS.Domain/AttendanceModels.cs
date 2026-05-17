namespace SchoolMS.Domain;

public class AttendanceRecord
{
    public int    StudentId{get;set;}
    public string FullName{get;set;}="";
    public string AdmissionNo{get;set;}="";
    public string? RollNo{get;set;}
    public string? ClassName{get;set;}
    public string? SectionName{get;set;}
    public string? BatchName{get;set;}
    public string? ProfilePicPath{get;set;}
    public string  AttendanceStatus{get;set;}="Present";
    public int?    AttendanceId{get;set;}
    public string? Remarks{get;set;}
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
}

public class DateAttendanceEntry
{
    public int      StudentId{get;set;}
    public DateTime AttendanceDate{get;set;}
    public string   Status{get;set;}="Present";
}
