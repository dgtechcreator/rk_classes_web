using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class TeacherAttendanceRepo(CommonConnectivity db)
{
    // SectionId (Stream / Medium) is created on first use - nullable, nothing existing changes (same as SQL/77).
    static volatile bool _ready;
    static readonly object _lock = new();
    void EnsureSchema()
    {
        if (_ready) return;
        lock (_lock)
        {
            if (_ready) return;
            db.ExecSql(@"
IF COL_LENGTH('dbo.TeacherAttendance', 'SectionId') IS NULL ALTER TABLE dbo.TeacherAttendance ADD SectionId INT NULL;
IF COL_LENGTH('dbo.TeacherAttendance', 'Source')     IS NULL ALTER TABLE dbo.TeacherAttendance ADD Source NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.TeacherAttendance', 'SourceKey')  IS NULL ALTER TABLE dbo.TeacherAttendance ADD SourceKey NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.TeacherAttendance', 'UpdatedBy')  IS NULL ALTER TABLE dbo.TeacherAttendance ADD UpdatedBy INT NULL;
IF COL_LENGTH('dbo.TeacherAttendance', 'UpdatedAt')  IS NULL ALTER TABLE dbo.TeacherAttendance ADD UpdatedAt DATETIME NULL;", new());
            _ready = true;
        }
    }

    // Values are bound as parameters (the old version pasted them into the SQL text, which turned Hindi
    // subject / topic text into '???' because the literals were not N'...' strings).
    public int Save(int facultyId, int? classId, int? batchId, int? sectionId, string? subject, string? topic, DateTime date,
        TimeSpan? inTime, TimeSpan? outTime, decimal? totalHours, int createdBy)
    {
        try
        {
            EnsureSchema();
            return db.SqlP(@"
                INSERT INTO TeacherAttendance (FacultyId, ClassId, BatchId, SectionId, SubjectName, Topic, AttendanceDate, InTime, OutTime, TotalHours, CreatedBy, CreatedAt, IsDeleted)
                OUTPUT INSERTED.AttendanceId
                VALUES (@Faculty, @Class, @Batch, @Section, @Subject, @Topic, @Date, @In, @Out, @Hours, @By, GETDATE(), 0)",
                new() {
                    {"@Faculty", facultyId}, {"@Class", classId}, {"@Batch", batchId}, {"@Section", sectionId},
                    {"@Subject", string.IsNullOrEmpty(subject) ? null : subject},
                    {"@Topic", string.IsNullOrEmpty(topic) ? null : topic},
                    {"@Date", date.Date}, {"@In", inTime}, {"@Out", outTime}, {"@Hours", totalHours}, {"@By", createdBy},
                },
                r => G.G<int>(r, "AttendanceId")).FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw new Exception($"Repository Save Error - Date: {date}, InTime: {inTime}, OutTime: {outTime}. Query Error: {ex.Message}", ex);
        }
    }

    const string SelectSql = @"
        SELECT ta.AttendanceId, ta.FacultyId, f.FullName AS FacultyName, ta.ClassId, c.ClassName,
            ta.BatchId, b.BatchName, ta.SectionId, sec.SectionName, ta.SubjectName, ta.Topic, ta.AttendanceDate,
            ta.InTime, ta.OutTime, ta.TotalHours, ta.CreatedAt, ta.CreatedBy, cu.FullName AS CreatedByName, ta.IsDeleted,
            ta.DeletedAt, ta.DeletedBy, du.FullName AS DeletedByName, ta.Source, ta.UpdatedBy, uu.FullName AS UpdatedByName, ta.UpdatedAt
        FROM TeacherAttendance ta
        LEFT JOIN Faculty f ON ta.FacultyId = f.FacultyId
        LEFT JOIN Classes c ON ta.ClassId = c.ClassId
        LEFT JOIN Batches b ON ta.BatchId = b.BatchId
        LEFT JOIN Sections sec ON ta.SectionId = sec.SectionId
        LEFT JOIN Users cu  ON cu.UserId = ta.CreatedBy
        LEFT JOIN Users du  ON du.UserId = ta.DeletedBy
        LEFT JOIN Users uu  ON uu.UserId = ta.UpdatedBy";

    /// <param name="createdBy">When set, only the rows entered by that login (a Class Representative sees just his own entries).</param>
    public List<TeacherAttendance> GetAll(int? facultyId = null, int? createdBy = null)
    {
        EnsureSchema();
        return db.SqlP(SelectSql + @"
            WHERE ta.IsDeleted = 0 AND ISNULL(f.Status,'Active') = 'Active'
              AND (@Faculty IS NULL OR ta.FacultyId = @Faculty)
              AND (@By IS NULL OR ta.CreatedBy = @By)
            ORDER BY ta.AttendanceDate DESC, ta.CreatedAt DESC",
            new() {{"@Faculty", facultyId}, {"@By", createdBy}}, MapTeacherAttendance);
    }

    public TeacherAttendance? GetById(int attendanceId)
        => EnsureSchemaThen(() => db.SqlP(SelectSql + " WHERE ta.AttendanceId = @Id AND ta.IsDeleted = 0",
            new() {{"@Id", attendanceId}}, MapTeacherAttendance).FirstOrDefault());

    T EnsureSchemaThen<T>(Func<T> f) { EnsureSchema(); return f(); }

    /// <summary>Entries of one teacher including the deleted ones (with who deleted them and when) - the teacher's own log.</summary>
    public List<TeacherAttendance> GetLogForFaculty(int facultyId)
    {
        EnsureSchema();
        return db.SqlP(SelectSql + @"
            WHERE ta.FacultyId = @Faculty
            ORDER BY ta.AttendanceDate DESC, ta.CreatedAt DESC",
            new() {{"@Faculty", facultyId}}, MapTeacherAttendance);
    }

    /// <summary>Edit an entry. Stamps who changed it and when (shown to the teacher).</summary>
    public void Update(int attendanceId, int facultyId, int? classId, int? batchId, int? sectionId, string? subject, string? topic, DateTime date,
        TimeSpan? inTime, TimeSpan? outTime, decimal? totalHours, int updatedBy)
    {
        EnsureSchema();
        db.ExecSql(@"UPDATE TeacherAttendance SET FacultyId=@Faculty, ClassId=@Class, BatchId=@Batch, SectionId=@Section, SubjectName=@Subject, Topic=@Topic,
                            AttendanceDate=@Date, InTime=@In, OutTime=@Out, TotalHours=@Hours, UpdatedBy=@By, UpdatedAt=GETDATE()
                     WHERE AttendanceId=@Id AND IsDeleted=0",
            new() {
                {"@Id", attendanceId}, {"@Faculty", facultyId}, {"@Class", classId}, {"@Batch", batchId}, {"@Section", sectionId},
                {"@Subject", string.IsNullOrEmpty(subject) ? null : subject}, {"@Topic", string.IsNullOrEmpty(topic) ? null : topic},
                {"@Date", date.Date}, {"@In", inTime}, {"@Out", outTime}, {"@Hours", totalHours}, {"@By", updatedBy},
            });
    }

    /// <summary>
    /// The teacher entry that follows from a student attendance save (teacher + subject + start / end time filled in there).
    /// One entry per lecture of one class / medium / batch (SourceKey). Never recreated once the admin deleted it, never
    /// overwritten once the admin edited it, and skipped when the same lecture was already typed by hand.
    /// Returns "created", "updated" or "" (nothing to do).
    /// </summary>
    public string UpsertFromStudentAttendance(string sourceKey, int facultyId, int? classId, int? batchId, int? sectionId, string? subject, DateTime date,
        TimeSpan inTime, TimeSpan outTime, decimal? totalHours, int by)
    {
        EnsureSchema();
        var existing = db.SqlP("SELECT TOP 1 AttendanceId, IsDeleted, UpdatedBy FROM TeacherAttendance WHERE SourceKey = @K",
            new() {{"@K", sourceKey}}, r => (id: G.G<int>(r, "AttendanceId"), deleted: G.G<bool>(r, "IsDeleted"), edited: G.G<int?>(r, "UpdatedBy") != null)).FirstOrDefault();
        if (existing.id != 0)
        {
            if (existing.deleted || existing.edited) return "";
            db.ExecSql(@"UPDATE TeacherAttendance SET FacultyId=@Faculty, SubjectName=@Subject, InTime=@In, OutTime=@Out, TotalHours=@Hours
                         WHERE AttendanceId=@Id AND (FacultyId<>@Faculty OR ISNULL(SubjectName,'')<>ISNULL(@Subject,'') OR ISNULL(InTime,'00:00')<>@In OR ISNULL(OutTime,'00:00')<>@Out)",
                new() {{"@Id", existing.id}, {"@Faculty", facultyId}, {"@Subject", string.IsNullOrEmpty(subject) ? null : subject}, {"@In", inTime}, {"@Out", outTime}, {"@Hours", totalHours}});
            return "updated";
        }
        var dup = db.SqlP(@"SELECT TOP 1 AttendanceId FROM TeacherAttendance
                            WHERE IsDeleted = 0 AND FacultyId=@Faculty AND AttendanceDate=@Date AND InTime=@In
                              AND ISNULL(ClassId,0)=ISNULL(@Class,0) AND ISNULL(BatchId,0)=ISNULL(@Batch,0) AND ISNULL(SectionId,0)=ISNULL(@Section,0)",
            new() {{"@Faculty", facultyId}, {"@Date", date.Date}, {"@In", inTime}, {"@Class", classId}, {"@Batch", batchId}, {"@Section", sectionId}},
            r => G.G<int>(r, "AttendanceId")).FirstOrDefault();
        if (dup != 0) return "";
        db.ExecSql(@"INSERT INTO TeacherAttendance (FacultyId, ClassId, BatchId, SectionId, SubjectName, AttendanceDate, InTime, OutTime, TotalHours, CreatedBy, CreatedAt, IsDeleted, Source, SourceKey)
                     VALUES (@Faculty, @Class, @Batch, @Section, @Subject, @Date, @In, @Out, @Hours, @By, GETDATE(), 0, 'student', @K)",
            new() {{"@Faculty", facultyId}, {"@Class", classId}, {"@Batch", batchId}, {"@Section", sectionId}, {"@Subject", string.IsNullOrEmpty(subject) ? null : subject},
                   {"@Date", date.Date}, {"@In", inTime}, {"@Out", outTime}, {"@Hours", totalHours}, {"@By", by}, {"@K", sourceKey}});
        return "created";
    }

    public void Delete(int attendanceId, int deletedBy)
        => db.ExecSql("UPDATE TeacherAttendance SET IsDeleted = 1, DeletedBy = @By, DeletedAt = GETDATE() WHERE AttendanceId = @Id",
            new() {{"@Id", attendanceId}, {"@By", deletedBy}});

    public List<DeletedItem> GetDeleted()
        => db.Sql(@"SELECT ta.AttendanceId, f.FullName AS FacultyName, ta.SubjectName, ta.Topic, ta.AttendanceDate, ta.DeletedAt,
                           u.FullName AS DeletedByName
                    FROM TeacherAttendance ta
                    LEFT JOIN Faculty f ON f.FacultyId = ta.FacultyId
                    LEFT JOIN Users u ON u.UserId = ta.DeletedBy
                    WHERE ta.IsDeleted = 1 ORDER BY ta.DeletedAt DESC",
            r => new DeletedItem {
                Kind = "teacherattendance", KindLabel = "Teacher attendance", Id = G.G<int>(r,"AttendanceId"),
                Title = G.G<string>(r,"FacultyName") ?? "Teacher",
                Subtitle = string.Join(" - ", new[] { G.G<DateTime>(r,"AttendanceDate").ToString("dd MMM yyyy"), G.G<string>(r,"SubjectName"), G.G<string>(r,"Topic") }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                DeletedAt = G.G<DateTime?>(r,"DeletedAt"), DeletedByName = G.G<string>(r,"DeletedByName") });

    public void Restore(int attendanceId)
        => db.ExecSql("UPDATE TeacherAttendance SET IsDeleted = 0, DeletedBy = NULL, DeletedAt = NULL WHERE AttendanceId = @Id",
            new() {{"@Id", attendanceId}});

    static TeacherAttendance MapTeacherAttendance(SqlDataReader r) => new()
    {
        AttendanceId = G.G<int>(r, "AttendanceId"),
        FacultyId = G.G<int>(r, "FacultyId"),
        FacultyName = G.G<string>(r, "FacultyName") ?? "",
        ClassId = G.G<int?>(r, "ClassId"),
        ClassName = G.G<string>(r, "ClassName") ?? "",
        BatchId = G.G<int?>(r, "BatchId"),
        BatchName = G.G<string>(r, "BatchName") ?? "",
        SectionId = G.G<int?>(r, "SectionId"),
        SectionName = G.G<string>(r, "SectionName") ?? "",
        SubjectName = G.G<string>(r, "SubjectName") ?? "",
        Topic = G.G<string>(r, "Topic") ?? "",
        AttendanceDate = G.G<DateTime>(r, "AttendanceDate"),
        InTime = G.G<TimeSpan?>(r, "InTime"),
        OutTime = G.G<TimeSpan?>(r, "OutTime"),
        TotalHours = G.G<decimal?>(r, "TotalHours"),
        CreatedAt = G.G<DateTime>(r, "CreatedAt"),
        CreatedBy = G.G<int?>(r, "CreatedBy"),
        CreatedByName = G.G<string>(r, "CreatedByName") ?? "",
        IsDeleted = G.G<bool>(r, "IsDeleted"),
        DeletedAt = G.G<DateTime?>(r, "DeletedAt"),
        DeletedBy = G.G<int?>(r, "DeletedBy"),
        DeletedByName = G.G<string>(r, "DeletedByName") ?? "",
        Source = G.G<string>(r, "Source") ?? "",
        UpdatedBy = G.G<int?>(r, "UpdatedBy"),
        UpdatedByName = G.G<string>(r, "UpdatedByName") ?? "",
        UpdatedAt = G.G<DateTime?>(r, "UpdatedAt"),
    };
}
