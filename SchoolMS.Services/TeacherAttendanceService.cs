using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class TeacherAttendanceService(TeacherAttendanceRepo repo, FacultyService facultySvc)
{
    public int Save(int facultyId, int? classId, int? batchId, string? subject, string? topic, DateTime date,
        TimeSpan? inTime, TimeSpan? outTime, int createdBy)
    {
        try
        {
            decimal? totalHours = null;
            if (inTime.HasValue && outTime.HasValue)
            {
                var timeSpan = outTime.Value - inTime.Value;
                if (timeSpan.TotalHours > 0)
                    totalHours = (decimal)timeSpan.TotalHours;
            }

            return repo.Save(facultyId, classId, batchId, subject, topic, date, inTime, outTime, totalHours, createdBy);
        }
        catch (Exception ex)
        {
            throw new Exception($"Service Save Error - Date: {date}, InTime: {inTime}, OutTime: {outTime}. {ex.Message}", ex);
        }
    }

    public List<TeacherAttendance> GetAll(int? facultyId = null) => repo.GetAll(facultyId);
    public TeacherAttendance? GetById(int attendanceId) => repo.GetById(attendanceId);
    public void Delete(int attendanceId, int deletedBy) => repo.Delete(attendanceId, deletedBy);

    public (decimal TotalHours, int TotalDays) GetSummary(int? facultyId = null)
    {
        var records = repo.GetAll(facultyId);
        var totalHours = records.Where(r => r.TotalHours.HasValue).Sum(r => r.TotalHours.Value);
        var totalDays = records.DistinctBy(r => r.AttendanceDate).Count();
        return (totalHours, totalDays);
    }
}
