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
            ClassName=G.G<string>(r,"ClassName"), SectionName=G.G<string>(r,"SectionName"),
            BatchName=G.G<string>(r,"BatchName"), ProfilePicPath=G.G<string>(r,"ProfilePicPath"),
            AttendanceStatus=G.G<string>(r,"AttendanceStatus")??"Present",
            AttendanceId=G.G<int?>(r,"AttendanceId"), Remarks=G.G<string>(r,"Remarks")
        });
    public void Save(int sid, DateTime date, string status, int? cls, int? sec, int? bat, string? remarks, int by)
        => db.Exec("sp_SaveAttendance", new() { { "@StudentId",sid }, { "@AttendanceDate",date.Date }, { "@Status",status },
            { "@ClassId",cls }, { "@SectionId",sec }, { "@BatchId",bat }, { "@Remarks",remarks }, { "@MarkedBy",by } });
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
            AttendancePct= G.G<decimal>(r,"AttendancePct")
        });
}
