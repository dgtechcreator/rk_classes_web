using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class StudentService(StudentRepo repo)
{
    public (List<Student> data, int total) GetAll(int pg, int ps, string? s, int? cls, int? sec, int? bat, int? yr, string? status)
        => repo.GetAll(pg, ps, s, cls, sec, bat, yr, status);
    public Student? GetById(int id) => repo.GetById(id);
    public int Save(Student s, string? pic, int by) => repo.Save(s, pic, by);
    public void SetPhoto(int id, string? path) => repo.SetPhoto(id, path);
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
