using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class AttendanceBatchService(AttendanceBatchRepo repo)
{
    public List<AttendanceBatch> GetAll() => repo.GetAll();

    public AttendanceBatch? GetById(int batchId) => repo.GetById(batchId);

    public int CreateBatch(string batchName, List<int> studentIds)
    {
        if (string.IsNullOrWhiteSpace(batchName) || !studentIds.Any())
            throw new Exception("Batch name and students are required.");

        var batch = new AttendanceBatch
        {
            BatchName = batchName.Trim(),
            StudentIds = studentIds
        };

        return repo.Save(batch);
    }

    public void DeleteBatch(int batchId) => repo.Delete(batchId);
}
