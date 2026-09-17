using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class TeacherAttendanceRepo(CommonConnectivity db)
{
    public int Save(int facultyId, int? classId, int? batchId, string? subject, string? topic, DateTime date,
        TimeSpan? inTime, TimeSpan? outTime, decimal? totalHours, int createdBy)
    {
        try
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            var inTimeStr = inTime.HasValue ? inTime.Value.ToString("hh\\:mm\\:ss") : "NULL";
            var outTimeStr = outTime.HasValue ? outTime.Value.ToString("hh\\:mm\\:ss") : "NULL";
            var subjectStr = string.IsNullOrEmpty(subject) ? "NULL" : $"'{subject.Replace("'", "''")}'";
            var topicStr = string.IsNullOrEmpty(topic) ? "NULL" : $"'{topic.Replace("'", "''")}'";
            var totalHoursStr = totalHours.HasValue ? totalHours.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "NULL";

            var inTimeSQL = inTime.HasValue ? $"'{inTimeStr}'" : inTimeStr;
            var outTimeSQL = outTime.HasValue ? $"'{outTimeStr}'" : outTimeStr;

            var query = $@"
                INSERT INTO TeacherAttendance (FacultyId, ClassId, BatchId, SubjectName, Topic, AttendanceDate, InTime, OutTime, TotalHours, CreatedBy, CreatedAt, IsDeleted)
                OUTPUT INSERTED.AttendanceId
                VALUES ({facultyId}, {(classId.HasValue ? classId.Value : "NULL")}, {(batchId.HasValue ? batchId.Value : "NULL")},
                    {subjectStr}, {topicStr},
                    '{dateStr}', {inTimeSQL}, {outTimeSQL}, {totalHoursStr},
                    {createdBy}, GETDATE(), 0)
            ";

            return db.Sql(query, r => G.G<int>(r, "AttendanceId")).FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw new Exception($"Repository Save Error - Date: {date}, InTime: {inTime}, OutTime: {outTime}. Query Error: {ex.Message}", ex);
        }
    }

    public List<TeacherAttendance> GetAll(int? facultyId = null)
    {
        var query = $@"
            SELECT ta.AttendanceId, ta.FacultyId, f.FullName AS FacultyName, ta.ClassId, c.ClassName,
                ta.BatchId, b.BatchName, ta.SubjectName, ta.Topic, ta.AttendanceDate,
                ta.InTime, ta.OutTime, ta.TotalHours, ta.CreatedAt, ta.CreatedBy, ta.IsDeleted,
                ta.DeletedAt, ta.DeletedBy
            FROM TeacherAttendance ta
            LEFT JOIN Faculty f ON ta.FacultyId = f.FacultyId
            LEFT JOIN Classes c ON ta.ClassId = c.ClassId
            LEFT JOIN Batches b ON ta.BatchId = b.BatchId
            WHERE ta.IsDeleted = 0 {(facultyId.HasValue ? $"AND ta.FacultyId = {facultyId}" : "")}
            ORDER BY ta.AttendanceDate DESC, ta.CreatedAt DESC
        ";

        return db.Sql(query, MapTeacherAttendance);
    }

    public TeacherAttendance? GetById(int attendanceId)
    {
        var query = $@"
            SELECT ta.AttendanceId, ta.FacultyId, f.FullName AS FacultyName, ta.ClassId, c.ClassName,
                ta.BatchId, b.BatchName, ta.SubjectName, ta.Topic, ta.AttendanceDate,
                ta.InTime, ta.OutTime, ta.TotalHours, ta.CreatedAt, ta.CreatedBy, ta.IsDeleted,
                ta.DeletedAt, ta.DeletedBy
            FROM TeacherAttendance ta
            LEFT JOIN Faculty f ON ta.FacultyId = f.FacultyId
            LEFT JOIN Classes c ON ta.ClassId = c.ClassId
            LEFT JOIN Batches b ON ta.BatchId = b.BatchId
            WHERE ta.AttendanceId = {attendanceId} AND ta.IsDeleted = 0
        ";

        return db.Sql(query, MapTeacherAttendance).FirstOrDefault();
    }

    public void Delete(int attendanceId, int deletedBy)
    {
        var query = $@"
            UPDATE TeacherAttendance
            SET IsDeleted = 1, DeletedBy = {deletedBy}, DeletedAt = GETDATE()
            WHERE AttendanceId = {attendanceId}
        ";

        db.Sql(query, r => true);
    }

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
        IsDeleted = G.G<bool>(r, "IsDeleted"),
        DeletedAt = G.G<DateTime?>(r, "DeletedAt"),
        DeletedBy = G.G<int?>(r, "DeletedBy")
    };
}
