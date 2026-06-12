using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class StudentService(StudentRepo repo)
{
    public (List<Student> data, int total) GetAll(int pg, int ps, string? s, int? cls, int? sec, int? bat, int? yr, string? status)
        => repo.GetAll(pg, ps, s, cls, sec, bat, yr, status);
    public Student? GetById(int id) => repo.GetById(id);
    public int Save(Student s, string? pic, int by) => repo.Save(s, pic, by);
    public void Delete(int id)  => repo.Delete(id);
    public void Restore(int id) => repo.Restore(id);
}
