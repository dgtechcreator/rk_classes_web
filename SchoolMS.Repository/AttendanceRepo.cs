using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class AttendanceRepo(CommonConnectivity db)
{
    public List<AttendanceRecord> GetForDate(DateTime date, int? cls, int? sec, int? bat)
        => db.Read("sp_GetAttendance", new() { { "@AttendanceDate",date.Date }, { "@ClassId",cls }, { "@SectionId",sec }, { "@BatchId",bat } },
        r => new AttendanceRecord {
            StudentId=G.G<int>(r,"StudentId"), FullName=G.G<string>(r,"FullName")??"",
            AdmissionNo=G.G<string>(r,"AdmissionNo")??"", RollNo=G.G<string>(r,"RollNo"),
            ClassId=G.G<int?>(r,"ClassId"), ClassName=G.G<string>(r,"ClassName"),
            SectionId=G.G<int?>(r,"SectionId"), SectionName=G.G<string>(r,"SectionName"),
            BatchId=G.G<int?>(r,"BatchId"), BatchName=G.G<string>(r,"BatchName"),
            ProfilePicPath=G.G<string>(r,"ProfilePicPath"),
            AttendanceStatus=G.G<string>(r,"AttendanceStatus")??"Present",
            AttendanceId=G.G<int?>(r,"AttendanceId"), Remarks=G.G<string>(r,"Remarks"),
            Subject=G.G<string>(r,"Subject"), SirName=G.G<string>(r,"SirName"),
            StartTime=G.G<TimeSpan?>(r,"StartTime"), EndTime=G.G<TimeSpan?>(r,"EndTime"),
            Phone=G.G<string>(r,"Phone"), FatherPhone=G.G<string>(r,"FatherPhone"), MotherPhone=G.G<string>(r,"MotherPhone")
        });
    public void Save(int sid, DateTime date, string status, int? cls, int? sec, int? bat,
        string? remarks, int by, string? subject, string? sirName, TimeSpan? startTime, TimeSpan? endTime)
        => db.Exec("sp_SaveAttendance", new() {
            { "@StudentId",sid }, { "@AttendanceDate",date.Date }, { "@Status",status },
            { "@ClassId",cls }, { "@SectionId",sec }, { "@BatchId",bat }, { "@Remarks",remarks }, { "@MarkedBy",by },
            { "@Subject",subject }, { "@SirName",sirName }, { "@StartTime",startTime }, { "@EndTime",endTime }
        });
    public (List<AttendanceRecord> students, List<DateAttendanceEntry> att) GetDateGrid(
        int? cls, int? sec, int? bat, DateTime from, DateTime to)
    {
        var p = new Dictionary<string, object?> {
            {"@ClassId",cls}, {"@SectionId",sec}, {"@BatchId",bat},
            {"@FromDate",from.Date}, {"@ToDate",to.Date}
        };

        var students = db.Read("sp_GetAttendanceDateGrid", p,
            r => new AttendanceRecord {
                StudentId   = G.G<int>(r,"StudentId"),
                FullName    = G.G<string>(r,"FullName")??"",
                AdmissionNo = G.G<string>(r,"AdmissionNo")??"",
                RollNo      = G.G<string>(r,"RollNo"),
                ClassName   = G.G<string>(r,"ClassName"),
                SectionName = G.G<string>(r,"SectionName"),
                BatchName   = G.G<string>(r,"BatchName")
            });

        var att = db.Sql($@"
            SELECT a.StudentId, a.AttendanceDate, a.Status
            FROM Attendance a
            INNER JOIN Students s ON s.StudentId = a.StudentId
            WHERE a.AttendanceDate BETWEEN '{from.Date:yyyy-MM-dd}' AND '{to.Date:yyyy-MM-dd}'
              {(cls.HasValue    ? $"AND s.ClassId={cls}"       : "")}
              {(sec.HasValue    ? $"AND s.SectionId={sec}"     : "")}
              {(bat.HasValue    ? $"AND s.BatchId={bat}"       : "")}",
            r => new DateAttendanceEntry {
                StudentId      = G.G<int>(r,"StudentId"),
                AttendanceDate = G.G<DateTime>(r,"AttendanceDate"),
                Status         = G.G<string>(r,"Status")??"Present"
            });

        return (students, att);
    }

    // Per-date records for a student — subject + teacher info
    public List<StudentAttendanceDetail> GetStudentAttendanceDetail(int studentId)
    {
        const string sql = @"
            SELECT a.AttendanceDate, a.Status,
                   ISNULL(a.Subject,'') AS Subject,
                   ISNULL(a.SirName,'') AS SirName,
                   ISNULL(a.Remarks,'') AS Remarks
            FROM   Attendance a
            WHERE  a.StudentId = @StudentId
            ORDER  BY a.AttendanceDate DESC";
        var list = new List<StudentAttendanceDetail>();
        using var c   = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@StudentId", studentId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new StudentAttendanceDetail {
                AttendanceDate = G.G<DateTime>(r,"AttendanceDate"),
                Status         = G.G<string>(r,"Status")??"Present",
                Subject        = G.G<string>(r,"Subject"),
                SirName        = G.G<string>(r,"SirName"),
                Remarks        = G.G<string>(r,"Remarks")
            });
        return list;
    }

    public AttendanceReport GetStudentAttendanceSummary(int studentId)
    {
        const string sql = @"
            SELECT
                SUM(CASE WHEN a.Status='Present' THEN 1 ELSE 0 END) AS PresentDays,
                SUM(CASE WHEN a.Status='Absent'  THEN 1 ELSE 0 END) AS AbsentDays,
                SUM(CASE WHEN a.Status='Late'    THEN 1 ELSE 0 END) AS LateDays,
                COUNT(*)                                              AS TotalDays,
                CASE WHEN COUNT(*) > 0
                     THEN CAST(SUM(CASE WHEN a.Status='Present' THEN 1 ELSE 0 END)
                               * 100.0 / COUNT(*) AS decimal(5,1))
                     ELSE 0 END AS AttendancePct
            FROM Attendance a
            WHERE a.StudentId = @StudentId";
        using var c   = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@StudentId", studentId);
        using var r = cmd.ExecuteReader();
        if (r.Read())
            return new AttendanceReport {
                StudentId     = studentId,
                PresentDays   = G.G<int>(r,"PresentDays"),
                AbsentDays    = G.G<int>(r,"AbsentDays"),
                LateDays      = G.G<int>(r,"LateDays"),
                TotalDays     = G.G<int>(r,"TotalDays"),
                AttendancePct = G.G<decimal>(r,"AttendancePct")
            };
        return new AttendanceReport { StudentId = studentId };
    }

    public List<AttendanceReport> GetReport(int? cls, int? month, int? year)
        => db.Read("sp_GetAttendanceReport", new() { {"@ClassId",cls}, {"@Month",month}, {"@Year",year} },
        r => new AttendanceReport {
            StudentId    = G.G<int>(r,"StudentId"),
            FullName     = G.G<string>(r,"FullName")??"",
            AdmissionNo  = G.G<string>(r,"AdmissionNo")??"",
            ClassName    = G.G<string>(r,"ClassName"),
            PresentDays  = G.G<int>(r,"PresentDays"),
            AbsentDays   = G.G<int>(r,"AbsentDays"),
            LateDays     = G.G<int>(r,"LateDays"),
            TotalDays    = G.G<int>(r,"TotalDays"),
            AttendancePct= G.G<decimal>(r,"AttendancePct"),
            CreatedByName= G.G<string>(r,"CreatedByName")
        });

    public List<(int StudentId, string FullName, string AdmissionNo, string Phone, string FatherPhone, string MotherPhone, string Medium, string ClassName, string SectionName, string BatchName)> GetAbsentStudentsToday()
        => GetStudentsTodayByStatus("Absent");

    public List<(int StudentId, string FullName, string AdmissionNo, string Phone, string FatherPhone, string MotherPhone, string Medium, string ClassName, string SectionName, string BatchName)> GetPresentStudentsToday()
        => GetStudentsTodayByStatus("Present");

    // status is only ever one of the two fixed literals above (never user input).
    private List<(int StudentId, string FullName, string AdmissionNo, string Phone, string FatherPhone, string MotherPhone, string Medium, string ClassName, string SectionName, string BatchName)> GetStudentsTodayByStatus(string status)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        return db.Sql($@"
            SELECT s.StudentId, s.FullName, s.AdmissionNo, s.Phone, s.FatherPhone, s.MotherPhone, sec.SectionName as Medium,
                   c.ClassName, sec.SectionName, b.BatchName
            FROM Attendance a
            JOIN Students s ON s.StudentId = a.StudentId
            LEFT JOIN Classes c ON c.ClassId = a.ClassId
            LEFT JOIN Sections sec ON sec.SectionId = a.SectionId
            LEFT JOIN Batches b ON b.BatchId = a.BatchId
            WHERE CAST(a.AttendanceDate AS DATE) = '{today}'
              AND a.Status = '{status}'
            ORDER BY s.FullName",
            r => (
                G.G<int>(r, "StudentId"),
                G.G<string>(r, "FullName") ?? "",
                G.G<string>(r, "AdmissionNo") ?? "",
                G.G<string>(r, "Phone") ?? "",
                G.G<string>(r, "FatherPhone") ?? "",
                G.G<string>(r, "MotherPhone") ?? "",
                G.G<string>(r, "Medium") ?? "",
                G.G<string>(r, "ClassName") ?? "",
                G.G<string>(r, "SectionName") ?? "",
                G.G<string>(r, "BatchName") ?? ""
            )).ToList();
    }
}
