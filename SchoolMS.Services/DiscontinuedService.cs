using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>Everything the admin needs to see about a student before taking them off the roll.</summary>
public class DiscontinueOverview
{
    public Student Student { get; set; } = new();
    public AttendanceReport? Attendance { get; set; }
    public List<TestMark> Marks { get; set; } = new();
    public FeePosition Fee { get; set; } = new();
    public decimal MarksPct { get; set; }
    public int TestsCount { get; set; }
}

/// <summary>
/// Discontinue a student: the reason and a fee snapshot (paid / pending) are saved, then the student is set Inactive, so they
/// vanish from every count, list and report (the same switch as Deleted Records - nothing is erased, they can be taken back).
/// </summary>
public class DiscontinuedService(DiscontinuedRepo repo, StudentService students, FeePositionService feePos,
    AttendanceService att, MarksService marks)
{
    public List<DiscontinuedStudent> GetAll() => repo.GetAll();

    /// <summary>Takes a discontinued student back: Active again, and the entry is marked as reverted (kept as history).</summary>
    public (bool ok, string? error) Revert(int entryId, string? note, int by, string byName)
    {
        var d = repo.GetAll().FirstOrDefault(x => x.Id == entryId);
        if (d == null) return (false, "Entry not found.");
        if (d.Returned) return (false, "This student is already taken back.");
        if (!d.CanRevert) return (false, "Only the latest entry of a student can be reverted.");
        students.Reactivate(d.StudentId);
        try { repo.MarkReverted(entryId, by, byName, note); }
        catch (Exception ex) { return (false, "The student is active again, but the entry could not be marked (" + ex.Message + ")."); }
        return (true, null);
    }

    public DiscontinueOverview? Overview(int studentId)
    {
        var s = students.GetById(studentId);
        if (s == null) return null;
        var m = marks.GetStudentAllMarks(studentId);
        var obtained = m.Where(x => x.MarksObtained.HasValue).ToList();
        var max = obtained.Sum(x => (decimal)x.MaxMarks);
        return new DiscontinueOverview {
            Student = s,
            Attendance = att.GetStudentAttendanceSummary(studentId),
            Marks = m,
            Fee = feePos.Calculate(s),
            TestsCount = obtained.Count,
            MarksPct = max > 0 ? Math.Round(obtained.Sum(x => x.MarksObtained!.Value) * 100m / max, 1) : 0,
        };
    }

    public (bool ok, string? error) Discontinue(int studentId, string? kind, string? reason, DateTime leftOn, int by, string byName)
    {
        if (!DiscontinueKind.IsValid(kind)) return (false, "Choose whether the student left or was removed.");
        reason = (reason ?? "").Trim();
        if (reason.Length < 3) return (false, "Please write the reason.");
        if (reason.Length > 1000) reason = reason[..1000];

        var o = Overview(studentId);
        if (o == null) return (false, "Student not found.");
        if (!string.Equals(o.Student.Status, "Active", StringComparison.OrdinalIgnoreCase)) return (false, "This student is already inactive.");

        var d = new DiscontinuedStudent {
            StudentId = studentId, Kind = kind!, Reason = reason, LeftOn = leftOn.Date,
            ClassName = (o.Student.ClassName ?? "").Trim(), SectionName = (o.Student.SectionName ?? "").Trim(), BatchName = (o.Student.BatchName ?? "").Trim(),
            TotalFees = o.Fee.NetTotal, Discount = o.Fee.Discount, Paid = o.Fee.Paid, Pending = o.Fee.Balance,
            AttendancePct = Math.Min(100m, o.Attendance?.AttendancePct ?? 0), MarksPct = o.MarksPct,
            RecordedBy = by, RecordedByName = byName,
        };

        students.Delete(studentId, by);          // Status = Inactive: gone from every count / list
        try { repo.Insert(d); }
        catch (Exception ex)
        {
            students.Restore(studentId);          // never leave a student removed without the record
            return (false, "Could not save the record (" + ex.Message + "). The student was not changed.");
        }
        return (true, null);
    }
}
