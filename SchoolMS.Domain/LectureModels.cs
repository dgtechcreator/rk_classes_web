namespace SchoolMS.Domain;

public static class LectureStatus
{
    public const string Scheduled = "Scheduled";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = { Scheduled, Completed, Cancelled };
}

/// <summary>One scheduled lecture: a subject taught by a teacher to a class (+ optional medium and batch) on a date.</summary>
public class Lecture
{
    public int       LectureId   { get; set; }
    public DateTime  LectureDate { get; set; }
    public TimeSpan  StartTime   { get; set; }
    public TimeSpan  EndTime     { get; set; }
    public int       ClassId     { get; set; }
    public string?   ClassName   { get; set; }
    /// <summary>null = every medium of the class.</summary>
    public int?      SectionId   { get; set; }
    public string?   SectionName { get; set; }
    /// <summary>null = every batch (Morning / Evening ...).</summary>
    public int?      BatchId     { get; set; }
    public string?   BatchName   { get; set; }
    public string    SubjectName { get; set; } = "";
    public int       FacultyId   { get; set; }
    public string?   FacultyName { get; set; }
    public string?   Topic       { get; set; }
    public string?   Remarks     { get; set; }
    public string    Status      { get; set; } = LectureStatus.Scheduled;
    public string?   StatusNote  { get; set; }
    public Guid?     SeriesId    { get; set; }

    public double DurationHours => Math.Max(0, (EndTime - StartTime).TotalHours);

    /// <summary>"10th • English • Morning" — parts that are "all" are left out.</summary>
    public string GroupLabel => string.Join(" • ",
        new[] { ClassName, SectionName, BatchName }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
}

/// <summary>Search criteria; every field is optional. The ForStudent* fields pick lectures that apply to one student.</summary>
public class LectureFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int?      ClassId { get; set; }
    public int?      SectionId { get; set; }
    public int?      BatchId { get; set; }
    public string?   Subject { get; set; }
    public int?      FacultyId { get; set; }
    public string?   Status { get; set; }

    public int? ForStudentClassId { get; set; }
    public int? ForStudentSectionId { get; set; }
    public int? ForStudentBatchId { get; set; }
    public bool ForStudent => ForStudentClassId.HasValue;
}

/// <summary>Create / edit request. With <see cref="Repeat"/> one lecture is created for every selected weekday up to <see cref="RepeatUntil"/>.</summary>
public class LectureSaveReq
{
    public int       LectureId   { get; set; }
    public DateTime  Date        { get; set; }
    public string    StartTime   { get; set; } = "";
    public string    EndTime     { get; set; } = "";
    public int       ClassId     { get; set; }
    public int?      SectionId   { get; set; }
    public int?      BatchId     { get; set; }
    public string    SubjectName { get; set; } = "";
    public int       FacultyId   { get; set; }
    public string?   Topic       { get; set; }
    public string?   Remarks     { get; set; }
    public bool      Repeat      { get; set; }
    /// <summary>DayOfWeek numbers: 0 = Sunday ... 6 = Saturday.</summary>
    public List<int> RepeatDays  { get; set; } = new();
    public DateTime? RepeatUntil { get; set; }
}

/// <summary>Counts for one teacher / class group / subject over a period.</summary>
public class LectureSummaryRow
{
    public string  Key        { get; set; } = "";
    public string  Label      { get; set; } = "";
    public string? Sub        { get; set; }
    public int?    FacultyId  { get; set; }
    public int     Scheduled  { get; set; }
    public int     Completed  { get; set; }
    public int     Cancelled  { get; set; }
    public int     Total      => Scheduled + Completed + Cancelled;
    public double  HoursHeld  { get; set; }
    /// <summary>Scheduled lectures whose date has already passed but were never marked done / cancelled.</summary>
    public int     NotUpdated { get; set; }
}
