using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class StudentService(StudentRepo repo)
{
    public (List<Student> data, int total) GetAll(int pg, int ps, string? s, int? cls, int? sec, int? bat, int? yr, string? status)
        => repo.GetAll(pg, ps, s, cls, sec, bat, yr, status);
    public Student? GetById(int id) => repo.GetById(id);
    /// <summary>
    /// Roll numbers are unique inside one Class + Medium (section). A blank roll is given the next free number, so
    /// attendance can always be called by roll. A typed roll that another active student already has is refused.
    /// </summary>
    public int Save(Student s, string? pic, int by)
    {
        if (s.ClassId != null && s.SectionId != null)
        {
            var roll = (s.RollNo ?? "").Trim();
            if (roll == "")
                s.RollNo = repo.NextRollNo(s.ClassId.Value, s.SectionId.Value, s.StudentId).ToString();
            else if (repo.RollNoTaken(s.ClassId.Value, s.SectionId.Value, roll, s.StudentId))
                throw new InvalidOperationException(RollTakenMessage(roll));
            else
                s.RollNo = roll;
        }
        return repo.Save(s, pic, by);
    }

    /// <summary>Error text when the typed roll number is already used in the same class and medium; null when it is free or blank.</summary>
    public string? CheckRollNo(Student s)
    {
        if (s.ClassId == null || s.SectionId == null) return null;
        var roll = (s.RollNo ?? "").Trim();
        if (roll == "") return null;
        return repo.RollNoTaken(s.ClassId.Value, s.SectionId.Value, roll, s.StudentId) ? RollTakenMessage(roll) : null;
    }

    static string RollTakenMessage(string roll) =>
        $"Roll No {roll} is already used by another student in this class and medium. Please give a different number (or leave it blank).";
    public void SetPhoto(int id, string? path) => repo.SetPhoto(id, path);
    public void SetBasicDetails(int id, DateTime dateOfBirth, string? email) => repo.SetBasicDetails(id, dateOfBirth, email);
    public void SetAgreedDiscount(int id, decimal amount, string? reason, int by) => repo.SetAgreedDiscount(id, amount, reason, by);
    /// <summary>Delete is two steps: an Active student becomes Inactive; deleting an Inactive student moves them to Deleted Records.</summary>
    public string Delete(int id, int by = 0)
    {
        var st = repo.GetById(id)?.Status ?? "";
        if (string.Equals(st, "Deleted", StringComparison.OrdinalIgnoreCase)) return "Deleted";
        if (string.Equals(st, "Inactive", StringComparison.OrdinalIgnoreCase)) { repo.MarkDeleted(id, by); return "Deleted"; }
        repo.Delete(id, by);
        return "Inactive";
    }
    public Dictionary<int, (DateTime? at, string? by)> GetDeletedInfo() => repo.GetDeletedInfo();
    /// <summary>Straight back to Active, whatever the current step (used when a discontinued student is taken back).</summary>
    public void Reactivate(int id) => repo.Restore(id);

    /// <summary>Restore goes one step back: Deleted -> Inactive, Inactive -> Active.</summary>
    public void Restore(int id)
    {
        if (string.Equals(repo.GetById(id)?.Status, "Deleted", StringComparison.OrdinalIgnoreCase)) repo.SetInactive(id);
        else repo.Restore(id);
    }
    public int SetBatch(IEnumerable<int> ids, int batchId) => repo.SetBatch(ids, batchId);
    public List<Student> GetByClassAndBatch(string className, string batchName)
        => repo.GetByClassAndBatch(className, batchName);
}
