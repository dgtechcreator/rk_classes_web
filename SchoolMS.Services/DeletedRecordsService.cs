using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// The "other" soft-deleted records shown on Deleted Records (expenses, teacher payments, teacher attendance entries,
/// lectures). Students, faculty, users and fee receipts are handled by their own services.
/// </summary>
public class DeletedRecordsService(ExpensesRepo expenses, TeacherPaymentRepo payments, TeacherAttendanceRepo attendance, LectureRepo lectures)
{
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
