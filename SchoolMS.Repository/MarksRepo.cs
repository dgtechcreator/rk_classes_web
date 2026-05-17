using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class MarksRepo(CommonConnectivity db)
{
    public List<StudentMarkRow> GetStudentsForEntry(int? cls, int? sec, int? bat, int? yearId = null)
        => db.Read("sp_GetStudentsForMarks",
            new() { {"@ClassId",cls}, {"@SectionId",sec}, {"@BatchId",bat}, {"@AcademicYearId",yearId} },
            r => new StudentMarkRow {
                StudentId   = G.G<int>(r,"StudentId"),
                FullName    = G.G<string>(r,"FullName")??"",
                AdmissionNo = G.G<string>(r,"AdmissionNo")??"",
                RollNo      = G.G<string>(r,"RollNo"),
                ClassName   = G.G<string>(r,"ClassName"),
                SectionName = G.G<string>(r,"SectionName"),
                BatchName   = G.G<string>(r,"BatchName")
            });

    public List<StudentMarkRow> GetStudentsWithMarks(int? cls, int? sec, int? bat, int subjectId, string examName, DateTime? testDate, int? yearId)
    {
        var students = GetStudentsForEntry(cls, sec, bat, yearId);
        var marks = db.Read("sp_GetMarksBySubject",
            new() { {"@SubjectId",subjectId}, {"@ClassId",cls}, {"@SectionId",sec},
                    {"@BatchId",bat}, {"@ExamName",examName},
                    {"@TestDate",testDate.HasValue?(object)testDate.Value.Date:DBNull.Value},
                    {"@AcademicYearId",yearId} },
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
            {"@MarksObtained",marks}, {"@MaxMarks",maxMarks},
            {"@IsAbsent",isAbsent}, {"@EnteredBy",by}
        });

    public List<TestMark> GetTestResult(string examName, int yearId, int? cls, int? sec, int? bat, int? sid, DateTime? testDate)
        => db.Read("sp_GetTestResult",
            new() { {"@ExamName",examName}, {"@AcademicYearId",yearId}, {"@ClassId",cls},
                    {"@SectionId",sec}, {"@BatchId",bat}, {"@StudentId",sid},
                    {"@TestDate",testDate.HasValue?(object)testDate.Value.Date:DBNull.Value} },
            r => new TestMark {
                StudentId     = G.G<int>(r,"StudentId"),
                FullName      = G.G<string>(r,"FullName")??"",
                AdmissionNo   = G.G<string>(r,"AdmissionNo")??"",
                RollNo        = G.G<string>(r,"RollNo"),
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
}
