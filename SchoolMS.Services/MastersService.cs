using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class MastersService(MastersRepo repo)
{
    public List<AcademicYear> GetYears()       => repo.GetYears();
    public int  SaveYear(AcademicYear m)       => repo.SaveYear(m);
    public void DeleteYear(int id)             => repo.DeleteYear(id);

    public List<Class> GetClasses()            => repo.GetClasses();
    public int  SaveClass(Class m)             => repo.SaveClass(m);
    public void DeleteClass(int id)            => repo.DeleteClass(id);

    public List<Section> GetSections()         => repo.GetSections();
    public int  SaveSection(Section m)         => repo.SaveSection(m);
    public void DeleteSection(int id)          => repo.DeleteSection(id);

    public List<Batch> GetBatches()            => repo.GetBatches();
    public int  SaveBatch(Batch m)             => repo.SaveBatch(m);
    public void DeleteBatch(int id)            => repo.DeleteBatch(id);

    public List<FeeType> GetFeeTypes()         => repo.GetFeeTypes();
    public int  SaveFeeType(FeeType m)         => repo.SaveFeeType(m);
    public void DeleteFeeType(int id)          => repo.DeleteFeeType(id);

    public List<Subject> GetSubjects()         => repo.GetSubjects();
    public int  SaveSubject(Subject m)         => repo.SaveSubject(m);
    public void DeleteSubject(int id)          => repo.DeleteSubject(id);

    public List<ExpenseCat> GetExpenseCategories()    => repo.GetExpenseCategories();
    public int  SaveExpenseCat(ExpenseCat m)          => repo.SaveExpenseCat(m);
    public void DeleteExpenseCat(int id)              => repo.DeleteExpenseCat(id);
}
