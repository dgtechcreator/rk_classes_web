using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// The "other" soft-deleted records shown on Deleted Records (expenses, teacher payments, teacher attendance entries,
/// lectures). Students, faculty, users and fee receipts are handled by their own services.
/// </summary>
public class DeletedRecordsService(ExpensesRepo expenses, TeacherPaymentRepo payments, TeacherAttendanceRepo attendance, LectureRepo lectures)
{
    /// <summary>Puts "deleted by / on" on the deleted students and faculty (the stored procedures do not return them).</summary>
    public void Enrich(List<Student> students, List<Faculty> faculty, StudentService studentSvc, FacultyService facultySvc)
    {
        var s = studentSvc.GetDeletedInfo();
        foreach (var x in students) if (s.TryGetValue(x.StudentId, out var i)) { x.DeletedAt = i.at; x.DeletedByName = i.by; }
        var f = facultySvc.GetDeletedInfo();
        foreach (var x in faculty) if (f.TryGetValue(x.FacultyId, out var i)) { x.DeletedAt = i.at; x.DeletedByName = i.by; }
    }

    public List<DeletedItem> GetOthers()
        => expenses.GetDeleted().Concat(payments.GetDeleted()).Concat(attendance.GetDeleted()).Concat(lectures.GetDeleted())
            .OrderByDescending(d => d.DeletedAt).ToList();

    /// <summary>Restores one record; false when the kind is unknown.</summary>
    public bool Restore(string kind, int id)
    {
        switch ((kind ?? "").ToLowerInvariant())
        {
            case "expense": expenses.Restore(id); return true;
            case "teacherpayment": payments.Restore(id); return true;
            case "teacherattendance": attendance.Restore(id); return true;
            case "lecture": lectures.Restore(id); return true;
            default: return false;
        }
    }
}
