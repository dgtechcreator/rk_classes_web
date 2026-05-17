using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class LookupService(LookupRepo repo)
{
    public List<AcademicYear> GetYears()           => repo.GetYears();
    public List<Class>        GetClasses()          => repo.GetClasses();
    public List<Section>      GetSections()         => repo.GetSections();
    public List<Batch>        GetBatches()          => repo.GetBatches();
    public List<FeeType>      GetFeeTypes()         => repo.GetFeeTypes();
    public List<ExpenseCat>   GetExpCats()          => repo.GetExpCats();
    public List<Subject>      GetSubjects(int c)    => repo.GetSubjects(c);
    public List<Exam>         GetExams()            => repo.GetExams();
    public DashboardStats     GetDashStats()        => repo.GetDashStats();
    public List<Student>      GetStudentDropdown()  => repo.GetStudentDropdown();
    public int?               GetCurrentYearId()    => repo.GetYears().FirstOrDefault(y => y.IsCurrent)?.YearId;
}
