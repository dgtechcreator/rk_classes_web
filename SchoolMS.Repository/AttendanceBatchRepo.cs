using SchoolMS.Domain;
using SchoolMS.DB;

namespace SchoolMS.Repository;

public class AttendanceBatchRepo(CommonConnectivity db)
{
    public List<AttendanceBatch> GetAll()
    {
        var batches = db.Read("SELECT BatchId, BatchName, CreatedAt, IsActive FROM AttendanceBatches WHERE IsActive=1 ORDER BY CreatedAt DESC",
            new Dictionary<string, object?>(),
            r => new AttendanceBatch {
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
        var batches = db.Read("SELECT BatchId, BatchName, CreatedAt, IsActive FROM AttendanceBatches WHERE BatchId=@Id AND IsActive=1",
            new() { { "@Id", batchId } },
            r => new AttendanceBatch {
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
            var sql = "INSERT INTO AttendanceBatches (BatchName, CreatedAt, IsActive) VALUES (@Name, GETDATE(), 1)";
            db.Exec(sql, new() { { "@Name", batch.BatchName } });

            var newBatch = db.Read("SELECT MAX(BatchId) as BatchId FROM AttendanceBatches",
                new Dictionary<string, object?>(),
                r => CommonConnectivity.G<int>(r, "BatchId")).FirstOrDefault();

            int id = newBatch;

            if (id > 0 && batch.StudentIds.Any())
            {
                foreach (var studentId in batch.StudentIds)
                {
                    db.Exec("INSERT INTO AttendanceBatchStudents (BatchId, StudentId) VALUES (@BatchId, @StudentId)",
                        new() { { "@BatchId", id }, { "@StudentId", studentId } });
                }
            }

            return id;
        }
        else
        {
            db.Exec("UPDATE AttendanceBatches SET BatchName=@Name WHERE BatchId=@Id",
                new() { { "@Name", batch.BatchName }, { "@Id", batch.BatchId } });
            return batch.BatchId;
        }
    }

    public void Delete(int batchId)
    {
        db.Exec("UPDATE AttendanceBatches SET IsActive=0 WHERE BatchId=@Id", new() { { "@Id", batchId } });
    }

    private List<int> GetBatchStudents(int batchId)
    {
        return db.Read("SELECT StudentId FROM AttendanceBatchStudents WHERE BatchId=@BatchId",
            new() { { "@BatchId", batchId } },
            r => CommonConnectivity.G<int>(r, "StudentId"));
    }
}
