using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class AttendanceService(AttendanceRepo repo)
{
    public List<AttendanceRecord> GetForDate(DateTime d, int? cls, int? sec, int? bat)
        => repo.GetForDate(d, cls, sec, bat);
    public void Save(int sid, DateTime d, string status, int? cls, int? sec, int? bat,
        string? remarks, int by, string? subject, string? sirName, TimeSpan? startTime, TimeSpan? endTime)
        => repo.Save(sid, d, status, cls, sec, bat, remarks, by, subject, sirName, startTime, endTime);
    public AttendanceReport GetStudentAttendanceSummary(int studentId)
        => repo.GetStudentAttendanceSummary(studentId);
    public List<StudentAttendanceDetail> GetStudentAttendanceDetail(int studentId)
        => repo.GetStudentAttendanceDetail(studentId);
    public List<AttendanceReport> GetReport(int? cls, int? month, int? year)
        => repo.GetReport(cls, month, year);
    public (List<AttendanceRecord> students, List<DateAttendanceEntry> att) GetDateGrid(
        int? cls, int? sec, int? bat, DateTime from, DateTime to)
        => repo.GetDateGrid(cls, sec, bat, from, to);
}
