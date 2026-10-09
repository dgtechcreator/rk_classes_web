using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class MarksRepo(CommonConnectivity db)
{
    // Load students by Class/Section/Batch only — NO academic year filter
    public List<StudentMarkRow> GetStudentsForEntry(int? cls, int? sec, int? bat, int? yearId = null)
        => db.Read("sp_GetStudentsForMarks",
            new() { {"@ClassId",cls}, {"@SectionId",sec}, {"@BatchId",bat}, {"@AcademicYearId",DBNull.Value} },
            r => new StudentMarkRow {
                StudentId   = G.G<int>(r,"StudentId"),
                FullName    = r.StudentName(db) ?? "",
                AdmissionNo = G.G<string>(r,"AdmissionNo")??"",
                RollNo      = G.G<string>(r,"RollNo"),
                ClassName   = G.G<string>(r,"ClassName"),
                SectionName = G.G<string>(r,"SectionName"),
                BatchName   = G.G<string>(r,"BatchName")
            });

    public List<StudentMarkRow> GetStudentsWithMarks(int? cls, int? sec, int? bat, int subjectId, string examName, DateTime? testDate, int? yearId)
    {
        var students = GetStudentsForEntry(cls, sec, bat, null);
        // Fetch marks with no date/year filter so existing marks always show
        var marks = db.Read("sp_GetMarksBySubject",
            new() { {"@SubjectId",subjectId}, {"@ClassId",cls}, {"@SectionId",sec},
                    {"@BatchId",bat}, {"@ExamName",examName},
                    {"@TestDate",DBNull.Value},
                    {"@AcademicYearId",DBNull.Value} },
            r => new { sid=G.G<int>(r,"StudentId"), m=G.G<decimal?>(r,"MarksObtained"), mx=G.G<int>(r,"MaxMarks"), g=G.G<string>(r,"Grade"), td=G.G<DateTime?>(r,"TestDate") });
        foreach (var s in students) {
            var mark = marks.FirstOrDefault(m => m.sid == s.StudentId);
            if (mark != null) {
                s.MarksObtained = mark.m;
                s.MaxMarks      = mark.mx;
                s.Grade         = mark.g;
                s.IsAbsent      = mark.g == "AB";
                s.TestDate      = mark.td;
            }
        }
        return students;
    }

    public void SaveMarkDirect(int sid, int subjectId, string examName, int yearId, int classId, DateTime? testDate, decimal? marks, int maxMarks, bool isAbsent, int by)
        => db.Exec("sp_SaveMark", new() {
            {"@StudentId",sid}, {"@SubjectId",subjectId}, {"@ExamName",examName},
            {"@AcademicYearId",yearId}, {"@ClassId",classId},
            {"@TestDate",testDate.HasValue?(object)testDate.Value.Date:DBNull.Value},
            {"@MarksObtained",marks.HasValue?(object)marks.Value:DBNull.Value}, {"@MaxMarks",maxMarks},
            {"@IsAbsent",isAbsent}, {"@EnteredBy",by}
        });

    public List<TestMark> GetTestResult(string examName, int yearId, int? cls, int? sec, int? bat, int? sid, DateTime? testDate)
    {
        // Direct SQL — bypasses sp_GetTestResult which has AcademicYearId bugs in old DB versions.
        // Finds marks for ALL active exams with this name (handles duplicate-exam case too).
        // Only returns rows that have actual marks OR are absent (Grade='AB') — no null-junk rows.
        const string sql = @"
            SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade, tm.EnteredAt,
                   s.FullName, s.AdmissionNo, s.RollNo,
                   sub.SubjectName, sub.SubjectCode, sub.SubjectId,
                   e.ExamName, e.ExamId, e.TestDate,
                   c.ClassId, c.ClassName, sec2.SectionId, sec2.SectionName, b.BatchId, b.BatchName, ay.YearName
            FROM   TestMarks tm
            INNER JOIN Students      s    ON s.StudentId    = tm.StudentId
            INNER JOIN Subjects      sub  ON sub.SubjectId  = tm.SubjectId
            INNER JOIN Exams         e    ON e.ExamId       = tm.ExamId
            INNER JOIN Classes       c    ON c.ClassId      = s.ClassId
            LEFT  JOIN AcademicYears ay   ON ay.YearId      = e.AcademicYearId
            LEFT  JOIN Sections      sec2 ON sec2.SectionId = s.SectionId
            LEFT  JOIN Batches       b    ON b.BatchId      = s.BatchId
            WHERE  e.ExamName = @ExamName
              AND  e.IsActive  = 1
              AND  (tm.MarksObtained IS NOT NULL OR tm.Grade = 'AB')
              AND  (@ClassId   IS NULL OR s.ClassId   = @ClassId)
              AND  (@SectionId IS NULL OR s.SectionId = @SectionId)
              AND  (@BatchId   IS NULL OR s.BatchId   = @BatchId)
              AND  (@StudentId IS NULL OR s.StudentId = @StudentId)
            ORDER BY s.RollNo, sub.SubjectName";

        var list = new List<TestMark>();
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@ExamName",  examName);
        cmd.Parameters.AddWithValue("@ClassId",   (object?)cls ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SectionId", (object?)sec ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BatchId",   (object?)bat ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@StudentId", (object?)sid ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TestMark {
                StudentId     = G.G<int>(r,"StudentId"),
                FullName      = r.StudentName(db) ?? "",
                AdmissionNo   = G.G<string>(r,"AdmissionNo")??"",
                RollNo        = G.G<string>(r,"RollNo"),
                ExamName      = G.G<string>(r,"ExamName"),
                TestDate      = G.G<DateTime?>(r,"TestDate"),
                EnteredAt     = G.G<DateTime?>(r,"EnteredAt"),
                ClassId       = G.G<int?>(r,"ClassId"),
                SectionId     = G.G<int?>(r,"SectionId"),
                BatchId       = G.G<int?>(r,"BatchId"),
                SubjectId     = G.G<int>(r,"SubjectId"),
                SubjectName   = G.G<string>(r,"SubjectName"),
                SubjectCode   = G.G<string>(r,"SubjectCode"),
                MarksObtained = G.G<decimal?>(r,"MarksObtained"),
                MaxMarks      = G.G<int>(r,"MaxMarks"),
                Grade         = G.G<string>(r,"Grade"),
                ClassName     = G.G<string>(r,"ClassName"),
                SectionName   = G.G<string>(r,"SectionName"),
                BatchName     = G.G<string>(r,"BatchName"),
                YearName      = G.G<string>(r,"YearName")
            });
        return list;
    }

    // All marks for a class — used in Report "All Entries" view (year+section+class selected, no specific exam)
    public List<TestMark> GetAllMarksForClass(int? cls, int? sec, int? bat, int? yearId)
    {
        const string sql = @"
            SELECT tm.MarkId, tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade, tm.EnteredBy, tm.EnteredAt,
                   s.FullName, s.AdmissionNo, s.RollNo,
                   sub.SubjectName, sub.SubjectCode, sub.SubjectId,
                   e.ExamName, e.ExamId, e.TestDate,
                   c.ClassId, c.ClassName, sec2.SectionId, sec2.SectionName, b.BatchId, b.BatchName, ay.YearName,
                   u.FullName AS EnteredByName
            FROM   TestMarks tm
            INNER JOIN Students      s    ON s.StudentId    = tm.StudentId
            INNER JOIN Subjects      sub  ON sub.SubjectId  = tm.SubjectId
            INNER JOIN Exams         e    ON e.ExamId       = tm.ExamId
            INNER JOIN Classes       c    ON c.ClassId      = s.ClassId
            LEFT  JOIN AcademicYears ay   ON ay.YearId      = e.AcademicYearId
            LEFT  JOIN Sections      sec2 ON sec2.SectionId = s.SectionId
            LEFT  JOIN Batches       b    ON b.BatchId      = s.BatchId
            LEFT  JOIN Users         u    ON u.UserId       = tm.EnteredBy
            WHERE  e.IsActive = 1
              AND  (tm.MarksObtained IS NOT NULL OR tm.Grade = 'AB')
              AND  (@ClassId   IS NULL OR s.ClassId   = @ClassId)
              AND  (@SectionId IS NULL OR s.SectionId = @SectionId)
              AND  (@BatchId   IS NULL OR s.BatchId   = @BatchId)
              AND  (@YearId    IS NULL OR e.AcademicYearId = @YearId)
            ORDER BY s.RollNo, s.FullName, e.ExamName, sub.SubjectName";

        var list = new List<TestMark>();
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@ClassId",   (object?)cls    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SectionId", (object?)sec    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BatchId",   (object?)bat    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@YearId",    (object?)yearId ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TestMark {
                MarkId        = G.G<int>(r,"MarkId"),
                StudentId     = G.G<int>(r,"StudentId"),
                FullName      = r.StudentName(db) ?? "",
                AdmissionNo   = G.G<string>(r,"AdmissionNo")??"",
                RollNo        = G.G<string>(r,"RollNo"),
                ExamId        = G.G<int>(r,"ExamId"),
                ExamName      = G.G<string>(r,"ExamName"),
                TestDate      = G.G<DateTime?>(r,"TestDate"),
                EnteredAt     = G.G<DateTime?>(r,"EnteredAt"),
                ClassId       = G.G<int?>(r,"ClassId"),
                SectionId     = G.G<int?>(r,"SectionId"),
                BatchId       = G.G<int?>(r,"BatchId"),
                SubjectId     = G.G<int>(r,"SubjectId"),
                SubjectName   = G.G<string>(r,"SubjectName"),
                SubjectCode   = G.G<string>(r,"SubjectCode"),
                MarksObtained = G.G<decimal?>(r,"MarksObtained"),
                MaxMarks      = G.G<int>(r,"MaxMarks"),
                Grade         = G.G<string>(r,"Grade"),
                ClassName     = G.G<string>(r,"ClassName"),
                SectionName   = G.G<string>(r,"SectionName"),
                BatchName     = G.G<string>(r,"BatchName"),
                YearName      = G.G<string>(r,"YearName"),
                EnteredBy     = G.G<int?>(r,"EnteredBy"),
                EnteredByName = G.G<string>(r,"EnteredByName")
            });
        return list;
    }

    // All marks for a single student across all exams — used in 360 Profile view
    public List<TestMark> GetStudentAllMarks(int studentId)
    {
        const string sql = @"
            SELECT tm.MarkId, tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade,
                   s.FullName, s.AdmissionNo, s.RollNo,
                   sub.SubjectName, sub.SubjectCode, sub.SubjectId,
                   e.ExamName, e.ExamId, e.TestDate,
                   c.ClassName, sec2.SectionName, b.BatchName, ay.YearName
            FROM   TestMarks tm
            INNER JOIN Students      s    ON s.StudentId    = tm.StudentId
            INNER JOIN Subjects      sub  ON sub.SubjectId  = tm.SubjectId
            INNER JOIN Exams         e    ON e.ExamId       = tm.ExamId
            INNER JOIN Classes       c    ON c.ClassId      = s.ClassId
            LEFT  JOIN AcademicYears ay   ON ay.YearId      = e.AcademicYearId
            LEFT  JOIN Sections      sec2 ON sec2.SectionId = s.SectionId
            LEFT  JOIN Batches       b    ON b.BatchId      = s.BatchId
            WHERE  tm.StudentId = @StudentId
              AND  e.IsActive   = 1
              AND  (tm.MarksObtained IS NOT NULL OR tm.Grade = 'AB')
            ORDER BY e.TestDate DESC, sub.SubjectName";
        var list = new List<TestMark>();
        using var c   = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@StudentId", studentId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TestMark {
                MarkId        = G.G<int>(r,"MarkId"),
                StudentId     = G.G<int>(r,"StudentId"),
                FullName      = r.StudentName(db) ?? "",
                AdmissionNo   = G.G<string>(r,"AdmissionNo")??"",
                RollNo        = G.G<string>(r,"RollNo"),
                ExamId        = G.G<int>(r,"ExamId"),
                ExamName      = G.G<string>(r,"ExamName"),
                TestDate      = G.G<DateTime?>(r,"TestDate"),
                SubjectId     = G.G<int>(r,"SubjectId"),
                SubjectName   = G.G<string>(r,"SubjectName"),
                SubjectCode   = G.G<string>(r,"SubjectCode"),
                MarksObtained = G.G<decimal?>(r,"MarksObtained"),
                MaxMarks      = G.G<int>(r,"MaxMarks"),
                Grade         = G.G<string>(r,"Grade"),
                ClassName     = G.G<string>(r,"ClassName"),
                SectionName   = G.G<string>(r,"SectionName"),
                BatchName     = G.G<string>(r,"BatchName"),
                YearName      = G.G<string>(r,"YearName")
            });
        return list;
    }

    // Returns per-student per-exam aggregated marks — used for Top 5 rankings
    public List<TopStudent> GetStudentRankings(int? yearId, int? classId, int? sectionId)
    {
        const string sql = @"
            SELECT
                s.StudentId, s.FullName, s.AdmissionNo, s.RollNo,
                c.ClassName, sec2.SectionName, b.BatchName, ay.YearName,
                e.ExamName,
                SUM(ISNULL(tm.MarksObtained, 0))  AS TotalObtained,
                SUM(tm.MaxMarks)                   AS TotalMax,
                CASE WHEN SUM(tm.MaxMarks) > 0
                     THEN CAST(SUM(ISNULL(tm.MarksObtained,0)) * 100.0
                               / SUM(tm.MaxMarks) AS decimal(5,2))
                     ELSE 0 END                    AS Percentage
            FROM   TestMarks tm
            INNER JOIN Students      s    ON s.StudentId    = tm.StudentId
            INNER JOIN Exams         e    ON e.ExamId       = tm.ExamId
            INNER JOIN Classes       c    ON c.ClassId      = s.ClassId
            LEFT  JOIN AcademicYears ay   ON ay.YearId      = e.AcademicYearId
            LEFT  JOIN Sections      sec2 ON sec2.SectionId = s.SectionId
            LEFT  JOIN Batches       b    ON b.BatchId      = s.BatchId
            WHERE  e.IsActive = 1
              AND  tm.MarksObtained IS NOT NULL
              AND  (@YearId    IS NULL OR e.AcademicYearId = @YearId)
              AND  (@ClassId   IS NULL OR s.ClassId        = @ClassId)
              AND  (@SectionId IS NULL OR s.SectionId      = @SectionId)
            GROUP BY s.StudentId, s.FullName, s.AdmissionNo, s.RollNo,
                     c.ClassName, sec2.SectionName, b.BatchName, ay.YearName, e.ExamName
            ORDER BY e.ExamName, Percentage DESC";

        var list = new List<TopStudent>();
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@YearId",    (object?)yearId    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ClassId",   (object?)classId   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SectionId", (object?)sectionId ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TopStudent {
                StudentId     = G.G<int>(r,"StudentId"),
                FullName      = r.StudentName(db) ?? "",
                AdmissionNo   = G.G<string>(r,"AdmissionNo")??"",
                RollNo        = G.G<string>(r,"RollNo"),
                ClassName     = G.G<string>(r,"ClassName"),
                SectionName   = G.G<string>(r,"SectionName"),
                BatchName     = G.G<string>(r,"BatchName"),
                YearName      = G.G<string>(r,"YearName"),
                ExamName      = G.G<string>(r,"ExamName"),
                TotalObtained = G.G<decimal>(r,"TotalObtained"),
                TotalMax      = G.G<decimal>(r,"TotalMax"),
                Percentage    = G.G<decimal>(r,"Percentage")
            });
        return list;
    }

    // Per-student per-subject aggregated — used for Subject-wise Top 5
    public List<TopStudent> GetSubjectWiseRankings(int? yearId, int? classId, int? sectionId)
    {
        const string sql = @"
            SELECT
                s.StudentId, s.FullName, s.AdmissionNo, s.RollNo,
                c.ClassName, sec2.SectionName, b.BatchName, ay.YearName,
                sub.SubjectName, sub.SubjectCode,
                SUM(ISNULL(tm.MarksObtained, 0))  AS TotalObtained,
                SUM(tm.MaxMarks)                   AS TotalMax,
                CASE WHEN SUM(tm.MaxMarks) > 0
                     THEN CAST(SUM(ISNULL(tm.MarksObtained,0)) * 100.0
                               / SUM(tm.MaxMarks) AS decimal(5,2))
                     ELSE 0 END                    AS Percentage
            FROM   TestMarks tm
            INNER JOIN Students      s    ON s.StudentId    = tm.StudentId
            INNER JOIN Subjects      sub  ON sub.SubjectId  = tm.SubjectId
            INNER JOIN Exams         e    ON e.ExamId       = tm.ExamId
            INNER JOIN Classes       c    ON c.ClassId      = s.ClassId
            LEFT  JOIN AcademicYears ay   ON ay.YearId      = e.AcademicYearId
            LEFT  JOIN Sections      sec2 ON sec2.SectionId = s.SectionId
            LEFT  JOIN Batches       b    ON b.BatchId      = s.BatchId
            WHERE  e.IsActive = 1
              AND  tm.MarksObtained IS NOT NULL
              AND  (@YearId    IS NULL OR e.AcademicYearId = @YearId)
              AND  (@ClassId   IS NULL OR s.ClassId        = @ClassId)
              AND  (@SectionId IS NULL OR s.SectionId      = @SectionId)
            GROUP BY s.StudentId, s.FullName, s.AdmissionNo, s.RollNo,
                     c.ClassName, sec2.SectionName, b.BatchName, ay.YearName,
                     sub.SubjectName, sub.SubjectCode
            ORDER BY sub.SubjectName, Percentage DESC";

        var list = new List<TopStudent>();
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@YearId",    (object?)yearId    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ClassId",   (object?)classId   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SectionId", (object?)sectionId ?? DBNull.Value);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TopStudent {
                StudentId     = G.G<int>(r,"StudentId"),
                FullName      = r.StudentName(db) ?? "",
                AdmissionNo   = G.G<string>(r,"AdmissionNo")??"",
                RollNo        = G.G<string>(r,"RollNo"),
                ClassName     = G.G<string>(r,"ClassName"),
                SectionName   = G.G<string>(r,"SectionName"),
                BatchName     = G.G<string>(r,"BatchName"),
                YearName      = G.G<string>(r,"YearName"),
                SubjectName   = G.G<string>(r,"SubjectName"),
                SubjectCode   = G.G<string>(r,"SubjectCode"),
                TotalObtained = G.G<decimal>(r,"TotalObtained"),
                TotalMax      = G.G<decimal>(r,"TotalMax"),
                Percentage    = G.G<decimal>(r,"Percentage")
            });
        return list;
    }

    public List<Exam> GetExamList(int? yearId, int? classId)
        => db.Read("sp_GetExamList", new() { {"@AcademicYearId",yearId}, {"@ClassId",classId} },
            r => new Exam {
                ExamId         = G.G<int>(r,"ExamId"),
                ExamName       = G.G<string>(r,"ExamName")??"",
                TestDate       = G.G<DateTime?>(r,"TestDate"),
                AcademicYearId = G.G<int>(r,"AcademicYearId"),
                ClassId        = G.G<int>(r,"ClassId"),
                ClassName      = G.G<string>(r,"ClassName"),
                YearName       = G.G<string>(r,"YearName"),
                EntryCount     = G.G<int>(r,"EntryCount")
            });

    public List<TestMark> GetAllTestsByYear(int yearId)
    {
        const string sql = @"
            SELECT tm.StudentId, tm.MarksObtained, tm.MaxMarks, tm.Grade, tm.EnteredAt,
                   s.FullName, s.AdmissionNo, s.RollNo,
                   sub.SubjectName, sub.SubjectCode, sub.SubjectId,
                   e.ExamName, e.ExamId, e.TestDate,
                   c.ClassId, c.ClassName, sec.SectionId, sec.SectionName, b.BatchId, b.BatchName, ay.YearName
            FROM   TestMarks tm
            INNER JOIN Students      s    ON s.StudentId    = tm.StudentId
            INNER JOIN Subjects      sub  ON sub.SubjectId  = tm.SubjectId
            INNER JOIN Exams         e    ON e.ExamId       = tm.ExamId
            INNER JOIN Classes       c    ON c.ClassId      = s.ClassId
            INNER JOIN AcademicYears ay   ON ay.YearId      = e.AcademicYearId
            LEFT  JOIN Sections      sec  ON sec.SectionId  = s.SectionId
            LEFT  JOIN Batches       b    ON b.BatchId      = s.BatchId
            WHERE  e.AcademicYearId = @YearId
            ORDER BY sub.SubjectName, e.ExamName, s.RollNo";

        var list = new List<TestMark>();
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@YearId", yearId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TestMark {
                StudentId     = G.G<int>(r,"StudentId"),
                FullName      = r.StudentName(db) ?? "",
                AdmissionNo   = G.G<string>(r,"AdmissionNo")??"",
                RollNo        = G.G<string>(r,"RollNo"),
                ExamName      = G.G<string>(r,"ExamName"),
                ExamId        = G.G<int>(r,"ExamId"),
                SubjectName   = G.G<string>(r,"SubjectName")??"",
                SubjectCode   = G.G<string>(r,"SubjectCode"),
                SubjectId     = G.G<int>(r,"SubjectId"),
                MarksObtained = G.G<decimal?>(r,"MarksObtained"),
                MaxMarks      = G.G<int>(r,"MaxMarks"),
                Grade         = G.G<string>(r,"Grade"),
                TestDate      = G.G<DateTime?>(r,"TestDate"),
                ClassName     = G.G<string>(r,"ClassName"),
                ClassId       = G.G<int>(r,"ClassId"),
                SectionName   = G.G<string>(r,"SectionName"),
                SectionId     = G.G<int?>(r,"SectionId"),
                BatchName     = G.G<string>(r,"BatchName"),
                BatchId       = G.G<int?>(r,"BatchId"),
                YearName      = G.G<string>(r,"YearName")
            });
        return list;
    }

    public List<dynamic> GetTop5StudentsInSubject(string subjectName, int? classId, int? sectionId)
    {
        if (!classId.HasValue) return new List<dynamic>();

        var allMarks = GetAllMarksForClass(classId, sectionId, null, null);

        var top5 = allMarks
            .Where(m => m.SubjectName == subjectName && m.MarksObtained.HasValue && m.MarksObtained > 0)
            .GroupBy(m => new { m.StudentId, m.FullName, m.ClassName })
            .Select(g => new {
                FullName = g.Key.FullName,
                ClassName = g.Key.ClassName,
                MarksObtained = g.Max(x => x.MarksObtained),
                MaxMarks = g.First().MaxMarks,
                Grade = g.OrderByDescending(x => x.MarksObtained).First().Grade
            })
            .OrderByDescending(x => x.MarksObtained)
            .Take(5)
            .Cast<dynamic>()
            .ToList();

        return top5;
    }
}
