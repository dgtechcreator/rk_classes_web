using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class TeacherAttendanceService(TeacherAttendanceRepo repo, FacultyService facultySvc)
{
    public int Save(int facultyId, int? classId, int? batchId, int? sectionId, string? subject, string? topic, DateTime date,
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

            return repo.Save(facultyId, classId, batchId, sectionId, subject, topic, date, inTime, outTime, totalHours, createdBy);
        }
        catch (Exception ex)
        {
            throw new Exception($"Service Save Error - Date: {date}, InTime: {inTime}, OutTime: {outTime}. {ex.Message}", ex);
        }
    }

    public List<TeacherAttendance> GetAll(int? facultyId = null, int? createdBy = null) => repo.GetAll(facultyId, createdBy);
    public List<TeacherAttendance> GetLogForFaculty(int facultyId) => repo.GetLogForFaculty(facultyId);

    static decimal? Hours(TimeSpan? i, TimeSpan? o) => i.HasValue && o.HasValue && (o.Value - i.Value).TotalHours > 0 ? (decimal)(o.Value - i.Value).TotalHours : null;

    public void Update(int attendanceId, int facultyId, int? classId, int? batchId, int? sectionId, string? subject, string? topic, DateTime date,
        TimeSpan? inTime, TimeSpan? outTime, int updatedBy)
        => repo.Update(attendanceId, facultyId, classId, batchId, sectionId, subject, topic, date, inTime, outTime, Hours(inTime, outTime), updatedBy);

    /// <summary>
    /// Student attendance was saved with a teacher, subject and start / end time: record that teacher's lecture as a teacher
    /// attendance entry too (one per lecture of a class / medium / batch). Never fails the student save.
    /// </summary>
    public void SyncFromStudentAttendance(DateTime date, int session, string? teacherName, string? subject, TimeSpan? start, TimeSpan? end,
        int? classId, int? sectionId, int? batchId, int? attBatchId, int by)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(teacherName) || !start.HasValue || !end.HasValue || end.Value <= start.Value) return;
            var faculty = facultySvc.GetAllActive().FirstOrDefault(f => string.Equals((f.FullName ?? "").Trim(), teacherName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (faculty == null) return;
            // ONE entry per saved lecture (never one per student / batch: that would count the teacher's hours several times)
            var key = $"att:{date:yyyyMMdd}:{attBatchId}:{classId}:{sectionId}:{batchId}:{session}";
            repo.UpsertFromStudentAttendance(key, faculty.FacultyId, classId, batchId, sectionId, subject, date, start.Value, end.Value, Hours(start, end), by);
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("TeacherAttendance sync failed: " + ex.Message); }
    }
    public TeacherAttendance? GetById(int attendanceId) => repo.GetById(attendanceId);
    public void Delete(int attendanceId, int deletedBy) => repo.Delete(attendanceId, deletedBy);

    public (decimal TotalHours, int TotalDays) GetSummary(int? facultyId = null, int? createdBy = null)
    {
        var records = repo.GetAll(facultyId, createdBy);
        var totalHours = records.Where(r => r.TotalHours.HasValue).Sum(r => r.TotalHours.Value);
        var totalDays = records.DistinctBy(r => r.AttendanceDate).Count();
        return (totalHours, totalDays);
    }
}
