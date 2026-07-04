using SchoolMS.Domain;
using SchoolMS.DB;

namespace SchoolMS.Repository;

public class AttendanceBatchRepo(CommonConnectivity db)
{
    public List<AttendanceBatch> GetAll()
    {
        var batches = db.Read("sp_GetAttendanceBatches", new(), r => new AttendanceBatch {
            BatchId = CommonConnectivity.G<int>(r, "BatchId"),
            BatchName = CommonConnectivity.G<string>(r, "BatchName") ?? "",
            CreatedAt = CommonConnectivity.G<DateTime>(r, "CreatedAt"),
            IsActive = CommonConnectivity.G<bool>(r, "IsActive")
        });

        foreach (var batch in batches)
        {
            batch.StudentIds = GetBatchStudents(batch.BatchId);
            batch.StudentCount = batch.StudentIds.Count;
        }

        return batches;
    }

    public AttendanceBatch? GetById(int batchId)
    {
        var batches = db.Read("sp_GetAttendanceBatchById", new() { { "@BatchId", batchId } }, r => new AttendanceBatch {
            BatchId = CommonConnectivity.G<int>(r, "BatchId"),
            BatchName = CommonConnectivity.G<string>(r, "BatchName") ?? "",
            CreatedAt = CommonConnectivity.G<DateTime>(r, "CreatedAt"),
            IsActive = CommonConnectivity.G<bool>(r, "IsActive")
        });

        var batch = batches.FirstOrDefault();
        if (batch != null)
        {
            batch.StudentIds = GetBatchStudents(batch.BatchId);
            batch.StudentCount = batch.StudentIds.Count;
        }

        return batch;
    }

    public int Save(AttendanceBatch batch)
    {
        if (batch.BatchId == 0)
        {
            var ids = db.Read("sp_InsertAttendanceBatch", new() { { "@BatchName", batch.BatchName } }, r => CommonConnectivity.G<int>(r, ""));
            int id = ids.FirstOrDefault();

            if (id > 0 && batch.StudentIds.Any())
            {
                foreach (var studentId in batch.StudentIds)
                {
                    db.Exec("sp_InsertBatchStudent", new() { { "@BatchId", id }, { "@StudentId", studentId } });
                }
            }

            return id;
        }
        else
        {
            db.Exec("sp_UpdateAttendanceBatch", new() { { "@BatchId", batch.BatchId }, { "@BatchName", batch.BatchName } });
            return batch.BatchId;
        }
    }

    public void Delete(int batchId)
    {
        db.Exec("sp_DeleteAttendanceBatch", new() { { "@BatchId", batchId } });
    }

    private List<int> GetBatchStudents(int batchId)
    {
        return db.Read("sp_GetBatchStudents", new() { { "@BatchId", batchId } }, r => CommonConnectivity.G<int>(r, "StudentId"));
    }
}
