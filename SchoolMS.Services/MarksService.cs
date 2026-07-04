using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class MarksService(MarksRepo repo)
{
    public List<StudentMarkRow> GetStudentsForEntry(int? cls, int? sec, int? bat, int? yearId = null)
        => repo.GetStudentsForEntry(cls, sec, bat, yearId);
    public List<StudentMarkRow> GetStudentsWithMarks(int? cls, int? sec, int? bat, int subjectId, string examName, DateTime? testDate, int? yearId)
        => repo.GetStudentsWithMarks(cls, sec, bat, subjectId, examName, testDate, yearId);
    public void SaveMarkDirect(int sid, int subjectId, string examName, int yr, int classId, DateTime? testDate, decimal? marks, int maxMarks, bool isAbsent, int by)
        => repo.SaveMarkDirect(sid, subjectId, examName, yr, classId, testDate, marks, maxMarks, isAbsent, by);
    public List<TestMark> GetTestResult(string examName, int yr, int? cls, int? sec, int? bat, int? sid, DateTime? testDate)
        => repo.GetTestResult(examName, yr, cls, sec, bat, sid, testDate);
    public List<TestMark> GetAllMarksForClass(int? cls, int? sec, int? bat, int? yearId)
        => repo.GetAllMarksForClass(cls, sec, bat, yearId);
    public List<TestMark> GetStudentAllMarks(int studentId)
        => repo.GetStudentAllMarks(studentId);
    public List<TopStudent> GetStudentRankings(int? yearId, int? classId, int? sectionId)
        => repo.GetStudentRankings(yearId, classId, sectionId);
    public List<TopStudent> GetSubjectWiseRankings(int? yearId, int? classId, int? sectionId)
        => repo.GetSubjectWiseRankings(yearId, classId, sectionId);
    public List<Exam> GetExamList(int? yearId, int? classId)
        => repo.GetExamList(yearId, classId);
    public List<TestMark> GetAllTestsByYear(int yearId)
        => repo.GetAllTestsByYear(yearId);
    public List<dynamic> GetTop5StudentsInSubject(string subjectName, int? classId, int? sectionId)
        => repo.GetTop5StudentsInSubject(subjectName, classId, sectionId);
}
