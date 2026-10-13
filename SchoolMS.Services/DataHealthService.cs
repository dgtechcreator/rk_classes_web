using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class DataHealthService(DataHealthRepo repo)
{
    public DataHealthReport Build() => repo.Build();
}
