namespace SchoolMS.Domain;

public class TeacherAttendance
{
    public int AttendanceId { get; set; }
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = "";
    public int? ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int? BatchId { get; set; }
    public string BatchName { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public string Topic { get; set; } = "";
    public DateTime AttendanceDate { get; set; }
    public TimeSpan? InTime { get; set; }
    public TimeSpan? OutTime { get; set; }
    public decimal? TotalHours { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }
}
