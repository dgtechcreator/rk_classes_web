namespace SchoolMS.Domain;

public static class NoteKind
{
    public const string Notes = "Notes";
    public const string Practice = "Practice";
    public static bool IsValid(string? k) => k == Notes || k == Practice;
}

/// <summary>A study file a teacher puts up for a class (notes, or the daily practice paper). SectionId / BatchId null = every medium / batch.</summary>
public class ClassNote
{
    public int       NoteId        { get; set; }
    public string    Kind          { get; set; } = NoteKind.Notes;
    public string    Title         { get; set; } = "";
    public string?   Description   { get; set; }
    public int       ClassId       { get; set; }
    public string?   ClassName     { get; set; }
    public int?      SectionId     { get; set; }
    public string?   SectionName   { get; set; }
    public int?      BatchId       { get; set; }
    public string?   BatchName     { get; set; }
    public string?   SubjectName   { get; set; }
    public string    FileName      { get; set; } = "";
    /// <summary>Stored file name inside App_Data/class-notes (never sent to clients).</summary>
    public string    StoredName    { get; set; } = "";
    public long      FileSize      { get; set; }
    /// <summary>The day a practice paper is for (notes: the upload day).</summary>
    public DateTime  PaperDate     { get; set; } = DateTime.Today;
    public int?      CreatedBy     { get; set; }
    public string?   CreatedByName { get; set; }
    public DateTime  CreatedAt     { get; set; } = DateTime.Now;

    public string GroupLabel => string.Join(" • ",
        new[] { ClassName, SectionName, BatchName }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
}

public class ClassNoteFilter
{
    public int?    ClassId   { get; set; }
    public int?    SectionId { get; set; }
    public int?    BatchId   { get; set; }
    public string? Kind      { get; set; }
    public string? Subject   { get; set; }
    /// <summary>Only the notes that apply to this one student (class + his medium + his batch, or "all" of those).</summary>
    public int?    ForStudentClassId   { get; set; }
    public int?    ForStudentSectionId { get; set; }
    public int?    ForStudentBatchId   { get; set; }
}
