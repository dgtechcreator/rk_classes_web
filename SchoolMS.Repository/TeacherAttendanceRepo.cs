using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class TeacherAttendanceRepo(CommonConnectivity db)
{
    // Values are bound as parameters (the old version pasted them into the SQL text, which turned Hindi
    // subject / topic text into '???' because the literals were not N'...' strings).
    public int Save(int facultyId, int? classId, int? batchId, string? subject, string? topic, DateTime date,
        TimeSpan? inTime, TimeSpan? outTime, decimal? totalHours, int createdBy)
    {
        try
        {
            return db.SqlP(@"
                INSERT INTO TeacherAttendance (FacultyId, ClassId, BatchId, SubjectName, Topic, AttendanceDate, InTime, OutTime, TotalHours, CreatedBy, CreatedAt, IsDeleted)
                OUTPUT INSERTED.AttendanceId
                VALUES (@Faculty, @Class, @Batch, @Subject, @Topic, @Date, @In, @Out, @Hours, @By, GETDATE(), 0)",
                new() {
                    {"@Faculty", facultyId}, {"@Class", classId}, {"@Batch", batchId},
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
            ta.BatchId, b.BatchName, ta.SubjectName, ta.Topic, ta.AttendanceDate,
            ta.InTime, ta.OutTime, ta.TotalHours, ta.CreatedAt, ta.CreatedBy, cu.FullName AS CreatedByName, ta.IsDeleted,
            ta.DeletedAt, ta.DeletedBy
        FROM TeacherAttendance ta
        LEFT JOIN Faculty f ON ta.FacultyId = f.FacultyId
        LEFT JOIN Classes c ON ta.ClassId = c.ClassId
        LEFT JOIN Batches b ON ta.BatchId = b.BatchId
        LEFT JOIN Users cu  ON cu.UserId = ta.CreatedBy";

    /// <param name="createdBy">When set, only the rows entered by that login (a Class Representative sees just his own entries).</param>
    public List<TeacherAttendance> GetAll(int? facultyId = null, int? createdBy = null)
    {
        return db.SqlP(SelectSql + @"
            WHERE ta.IsDeleted = 0 AND ISNULL(f.Status,'Active') = 'Active'
              AND (@Faculty IS NULL OR ta.FacultyId = @Faculty)
              AND (@By IS NULL OR ta.CreatedBy = @By)
            ORDER BY ta.AttendanceDate DESC, ta.CreatedAt DESC",
            new() {{"@Faculty", facultyId}, {"@By", createdBy}}, MapTeacherAttendance);
    }

    public TeacherAttendance? GetById(int attendanceId)
        => db.SqlP(SelectSql + " WHERE ta.AttendanceId = @Id AND ta.IsDeleted = 0",
            new() {{"@Id", attendanceId}}, MapTeacherAttendance).FirstOrDefault();

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
        DeletedBy = G.G<int?>(r, "DeletedBy")
    };
}
