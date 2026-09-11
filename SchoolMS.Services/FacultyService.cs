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
    public void                 Delete(int id)                       => repo.Delete(id);
    public void                 Restore(int id)                      => repo.Restore(id);
    public List<Designation>    GetDesignations()                    => repo.GetDesignations();
    public int                  SaveDesignation(Designation d)       => repo.SaveDesignation(d);
    public List<Faculty>        GetAllActive()
    {
        var (data, _) = GetAll(null, "Active", null, 1, 1000);
        return data;
    }
}
