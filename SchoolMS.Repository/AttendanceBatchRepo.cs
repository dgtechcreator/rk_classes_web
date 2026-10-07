using SchoolMS.Domain;
using SchoolMS.DB;
using Microsoft.Data.SqlClient;

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

    /// <summary>Renames the batch and replaces its student list in one transaction (edit must never create a new batch).</summary>
    public void Update(int batchId, string batchName, List<int> studentIds)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();

        using (var cmd = new SqlCommand("UPDATE AttendanceBatches SET BatchName=@n WHERE BatchId=@id AND IsActive=1", c, tx))
        {
            cmd.Parameters.AddWithValue("@n", batchName);
            cmd.Parameters.AddWithValue("@id", batchId);
            if (cmd.ExecuteNonQuery() == 0) throw new Exception("Batch not found.");
        }
        using (var cmd = new SqlCommand("DELETE FROM AttendanceBatchStudents WHERE BatchId=@id", c, tx))
        {
            cmd.Parameters.AddWithValue("@id", batchId);
            cmd.ExecuteNonQuery();
        }
        foreach (var studentId in studentIds.Distinct())
        {
            using var cmd = new SqlCommand("INSERT INTO AttendanceBatchStudents (BatchId, StudentId) VALUES (@b, @s)", c, tx);
            cmd.Parameters.AddWithValue("@b", batchId);
            cmd.Parameters.AddWithValue("@s", studentId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
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
