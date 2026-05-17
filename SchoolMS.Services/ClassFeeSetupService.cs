using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class ClassFeeSetupService(ClassFeeSetupRepo repo)
{
    public List<ClassFeeSetup> GetAll()                             => repo.GetAll();
    public int                 Save(ClassFeeSetup fs)               => repo.Save(fs);
    public void                Delete(int id)                       => repo.Delete(id);
    public decimal?            GetAmount(int bat, int cls, int sec) => repo.GetAmount(bat, cls, sec);
}
