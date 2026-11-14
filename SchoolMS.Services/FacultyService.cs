using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class FacultyService(FacultyRepo repo)
{
    public (List<Faculty> data, int total) GetAll(string? search, string? status, int? desig, int pg, int ps)
        => repo.GetAll(search, status, desig, pg, ps);
    public Faculty?             GetById(int id)                      => repo.GetById(id);
    public List<FacultySubject> GetSubjects(int id)                  => repo.GetSubjects(id);
    public int                  Save(Faculty f, string? pic, int by) => repo.Save(f, pic, by);
    public void                 SetPhoto(int id, string? path)       => repo.SetPhoto(id, path);
    public void                 Delete(int id, int by = 0)           => repo.Delete(id, by);
    public Dictionary<int, (DateTime? at, string? by)> GetDeletedInfo() => repo.GetDeletedInfo();
    public void                 Restore(int id)                      => repo.Restore(id);
    public List<Designation>    GetDesignations()                    => repo.GetDesignations();
    public int                  SaveDesignation(Designation d)       => repo.SaveDesignation(d);
    public List<Faculty>        GetAllActive()
    {
        var (data, _) = GetAll(null, "Active", null, 1, 1000);
        return data;
    }

    /// <summary>
    /// Attendance rows store the bare faculty name. Students / parents should see it the way staff pick it
    /// ("Rakesh Mishra SIR" / "... MA'AM"), so swap in the Faculty display name. Unknown names are left as they are.
    /// </summary>
    public void ApplyTeacherLabels(IEnumerable<StudentAttendanceDetail> rows)
    {
        var list = rows.Where(r => !string.IsNullOrWhiteSpace(r.SirName)).ToList();
        if (list.Count == 0) return;
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in GetAllActive())
            if (!string.IsNullOrWhiteSpace(f.FullName)) byName[f.FullName.Trim()] = f.DisplayName;
        foreach (var r in list)
            if (byName.TryGetValue(r.SirName!.Trim(), out var label)) r.SirName = label;
    }
}
