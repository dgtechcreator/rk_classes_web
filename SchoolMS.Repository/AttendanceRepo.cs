using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class AttendanceRepo(CommonConnectivity db)
{
    // Session 1 is sent without @SessionNo, so the first lecture of the day still works before SQL/75 has been run.
    static Dictionary<string, object?> SessionParam(Dictionary<string, object?> p, int session)
    {
        if (session > 1) p["@SessionNo"] = session;
        return p;
    }

    public List<AttendanceRecord> GetForDate(DateTime date, int? cls, int? sec, int? bat, int session = 1)
        => db.Read("sp_GetAttendance", SessionParam(new() { { "@AttendanceDate",date.Date }, { "@ClassId",cls }, { "@SectionId",sec }, { "@BatchId",bat } }, session),
        r => new AttendanceRecord {
            SessionNo=session,
            StudentId=G.G<int>(r,"StudentId"), FullName=r.StudentName(db) ?? "",
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
        }).OrderBy(a => RollKey(a.RollNo)).ThenBy(a => a.FullName, StringComparer.OrdinalIgnoreCase).ToList();

    // Roll numbers sort numerically; students without one come after, ordered by name.
    static int RollKey(string? roll) => int.TryParse(roll?.Trim(), out var n) ? n : int.MaxValue;
    public void Save(int sid, DateTime date, string status, int? cls, int? sec, int? bat,
        string? remarks, int by, string? subject, string? sirName, TimeSpan? startTime, TimeSpan? endTime, int session = 1)
        => db.Exec("sp_SaveAttendance", SessionParam(new() {
            { "@StudentId",sid }, { "@AttendanceDate",date.Date }, { "@Status",status },
            { "@ClassId",cls }, { "@SectionId",sec }, { "@BatchId",bat }, { "@Remarks",remarks }, { "@MarkedBy",by },
            { "@Subject",subject }, { "@SirName",sirName }, { "@StartTime",startTime }, { "@EndTime",endTime }
        }, session));

    /// <summary>Lectures that already have attendance on a date: session number and how many students were marked.</summary>
    public List<(int Session, int Marked)> GetSessionsForDate(DateTime date)
    {
        try
        {
            return db.Sql($@"
                SELECT a.SessionNo, COUNT(*) AS Marked
                FROM Attendance a JOIN Students s ON s.StudentId = a.StudentId
                WHERE a.AttendanceDate = '{date.Date:yyyy-MM-dd}' AND s.Status = 'Active' AND s.AcademicYearId = {CurrentYear.Id}
                GROUP BY a.SessionNo ORDER BY a.SessionNo",
                r => (G.G<int>(r, "SessionNo"), G.G<int>(r, "Marked")));
        }
        catch { return new(); }   // SQL/75 not run yet: there is only the one (first) session
    }
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
                FullName    = r.StudentName(db) ?? "",
                AdmissionNo = G.G<string>(r,"AdmissionNo")??"",
                RollNo      = G.G<string>(r,"RollNo"),
                ClassName   = G.G<string>(r,"ClassName"),
                SectionName = G.G<string>(r,"SectionName"),
                BatchName   = G.G<string>(r,"BatchName")
            }).OrderBy(a => RollKey(a.RollNo)).ThenBy(a => a.FullName, StringComparer.OrdinalIgnoreCase).ToList();

        // SessionNo exists once SQL/75 has run; before that every row is the one (first) session.
        Func<string, List<DateAttendanceEntry>> loadAtt = sessionCol => db.Sql($@"
            SELECT a.StudentId, a.AttendanceDate, a.Status{sessionCol}
            FROM Attendance a
            INNER JOIN Students s ON s.StudentId = a.StudentId
            WHERE s.Status = 'Active' AND s.AcademicYearId = {CurrentYear.Id} AND a.AttendanceDate BETWEEN '{from.Date:yyyy-MM-dd}' AND '{to.Date:yyyy-MM-dd}'
              {(cls.HasValue    ? $"AND s.ClassId={cls}"       : "")}
              {(sec.HasValue    ? $"AND s.SectionId={sec}"     : "")}
              {(bat.HasValue    ? $"AND s.BatchId={bat}"       : "")}",
            r => new DateAttendanceEntry {
                StudentId      = G.G<int>(r,"StudentId"),
                AttendanceDate = G.G<DateTime>(r,"AttendanceDate"),
                Status         = G.G<string>(r,"Status")??"Present",
                SessionNo      = sessionCol.Length > 0 ? G.G<int>(r,"SessionNo") : 1
            });
        List<DateAttendanceEntry> att;
        try { att = loadAtt(", a.SessionNo"); }
        catch { att = loadAtt(""); }

        return (students, att);
    }

    // Per-date records for a student — subject + teacher info
    public List<StudentAttendanceDetail> GetStudentAttendanceDetail(int studentId)
    {
        try { return LoadAttendanceDetail(studentId, ", a.SessionNo", ", a.SessionNo"); }
        catch { return LoadAttendanceDetail(studentId, "", ""); }   // SQL/75 not run yet
    }

    List<StudentAttendanceDetail> LoadAttendanceDetail(int studentId, string sessionCol, string sessionOrder)
    {
        var sql = $@"
            SELECT a.AttendanceDate, a.Status{sessionCol},
                   ISNULL(a.Subject,'') AS Subject,
                   ISNULL(a.SirName,'') AS SirName,
                   ISNULL(a.Remarks,'') AS Remarks
            FROM   Attendance a
            WHERE  a.StudentId = @StudentId
            ORDER  BY a.AttendanceDate DESC{sessionOrder}";
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
                Remarks        = G.G<string>(r,"Remarks"),
                SessionNo      = sessionCol.Length > 0 ? G.G<int>(r,"SessionNo") : 1
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
            FullName     = r.StudentName(db) ?? "",
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
              AND s.Status = 'Active' AND s.AcademicYearId = {CurrentYear.Id}
              AND a.Status = '{status}'
              {(status == "Absent"
                  ? "AND NOT EXISTS (SELECT 1 FROM Attendance p WHERE p.StudentId = a.StudentId AND p.AttendanceDate = a.AttendanceDate AND p.Status = 'Present')"
                  : "")}
            ORDER BY s.FullName",
            r => (
                G.G<int>(r, "StudentId"),
                r.StudentName(db) ?? "",
                G.G<string>(r, "AdmissionNo") ?? "",
                G.G<string>(r, "Phone") ?? "",
                G.G<string>(r, "FatherPhone") ?? "",
                G.G<string>(r, "MotherPhone") ?? "",
                G.G<string>(r, "Medium") ?? "",
                G.G<string>(r, "ClassName") ?? "",
                G.G<string>(r, "SectionName") ?? "",
                G.G<string>(r, "BatchName") ?? ""
            )).GroupBy(x => x.Item1).Select(g => g.First())   // a student marked in two lectures is listed once
              .OrderBy(x => x.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
