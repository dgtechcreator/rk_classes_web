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

    public void UpdateBatch(int batchId, string batchName, List<int> studentIds)
    {
        if (string.IsNullOrWhiteSpace(batchName) || studentIds == null || !studentIds.Any())
            throw new Exception("Batch name and students are required.");
        repo.Update(batchId, batchName.Trim(), studentIds.Distinct().ToList());
    }

    public void DeleteBatch(int batchId) => repo.Delete(batchId);

    /// <summary>
    /// Per-batch present/total for one date. <paramref name="dayRecords"/> must be the full
    /// sp_GetAttendance result for that date (every active student, AttendanceId set once marked) so the
    /// totals only count active students and use the exact same rows the entry screen shows.
    /// </summary>
    public List<AttendanceBatchSummary> BuildSummaries(List<AttendanceBatch> batches, List<AttendanceRecord> dayRecords)
    {
        var byStudent = dayRecords.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());
        return batches.Select(b =>
        {
            var rows = b.StudentIds.Distinct().Where(byStudent.ContainsKey).Select(id => byStudent[id]).ToList();
            var marked = rows.Where(r => r.AttendanceId.HasValue).ToList();
            return new AttendanceBatchSummary
            {
                BatchId   = b.BatchId,
                BatchName = b.BatchName,
                Total     = rows.Count,
                Marked    = marked.Count,
                Present   = marked.Count(r => r.AttendanceStatus == "Present"),
                Absent    = marked.Count(r => r.AttendanceStatus == "Absent"),
                Late      = marked.Count(r => r.AttendanceStatus == "Late"),
            };
        }).ToList();
    }

    /// <summary>The batch's active students for the date, in roll-number order (same order as sp_GetAttendance).</summary>
    public List<AttendanceRecord> RecordsFor(AttendanceBatch batch, List<AttendanceRecord> dayRecords)
    {
        var ids = batch.StudentIds.ToHashSet();
        return dayRecords.Where(r => ids.Contains(r.StudentId)).GroupBy(r => r.StudentId).Select(g => g.First()).ToList();
    }
}
