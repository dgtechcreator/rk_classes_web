namespace SchoolMS.Domain;

/// <summary>Kinds of student activity a parent is told about. The apps pick the icon / colour from this.</summary>
public static class ActivityType
{
    public const string Absent = "absent";
    public const string Present = "present";
    public const string Late = "late";
    public const string Marks = "marks";
    public const string Fee = "fee";
    public const string Notes = "notes";
    public const string Practice = "practice";
    public const string Lecture = "lecture";
    public const string LectureUpdate = "lecture_update";
    public const string LectureCancel = "lecture_cancel";
    public const string LectureDone = "lecture_done";
}

/// <summary>One thing that happened to one student (marked absent, marks entered, fee received, lecture scheduled...).</summary>
public class StudentActivity
{
    public int      ActivityId  { get; set; }
    public int      StudentId   { get; set; }
    public string   StudentName { get; set; } = "";
    public string?  ClassName   { get; set; }
    public string   Kind        { get; set; } = "";
    public string   Title       { get; set; } = "";
    public string   Message     { get; set; } = "";
    public string?  DedupeKey   { get; set; }
    public int?     ReferenceId { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.Now;
    public bool     IsUnread    { get; set; }
}
