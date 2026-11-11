using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// Holiday for ONE attendance batch on a date ("today there is no class for this batch"). The batch card turns blue, attendance cannot be
/// saved for it that day, and the parents of the batch's students get a "No class today" notification in the app and on the website.
/// Taking the holiday back tells them the class is on.
/// </summary>
public class BatchHolidayService(BatchHolidayRepo repo, AttendanceBatchService batches, AttendanceService attendance, ActivityNotifier notifier)
{
    /// <summary>batchId -> holiday, for one date. Never throws (no holidays when the table cannot be read).</summary>
    public Dictionary<int, BatchHoliday> ForDate(DateTime date)
    {
        try { return repo.GetForDate(date).GroupBy(h => h.BatchId).ToDictionary(g => g.Key, g => g.First()); }
        catch { return new(); }
    }

    public (bool ok, string? error, int notified) Mark(int batchId, DateTime date, string? reason, int by, string byName)
    {
        date = date.Date;
        if (date > DateTime.Today) return (false, "A holiday can be marked for today or an earlier day only.", 0);
        var batch = batches.GetById(batchId);
        if (batch == null) return (false, "Attendance batch not found.", 0);
        if (ForDate(date).ContainsKey(batchId)) return (false, "This batch is already on holiday for that day.", 0);

        // Attendance already taken for this batch that day: a holiday would contradict it.
        var ids = batch.StudentIds.ToHashSet();
        foreach (var (session, _) in attendance.GetSessionsForDate(date))
            if (attendance.GetForDate(date, null, null, null, session).Any(r => ids.Contains(r.StudentId) && r.AttendanceId.HasValue))
                return (false, "Attendance is already marked for this batch on that day, so it cannot be a holiday.", 0);

        repo.Add(batchId, date, reason, by, byName);
        int n = 0;
        if (date == DateTime.Today)   // an old day must not send parents "no class" messages
            foreach (var id in ids) { notifier.BatchHoliday(id, batchId, batch.BatchName, date, reason, false); n++; }
        return (true, null, n);
    }

    public (bool ok, string? error, int notified) Cancel(int batchId, DateTime date)
    {
        date = date.Date;
        var batch = batches.GetById(batchId);
        if (batch == null) return (false, "Attendance batch not found.", 0);
        if (!ForDate(date).ContainsKey(batchId)) return (false, "There is no holiday to cancel.", 0);
        repo.Remove(batchId, date);
        int n = 0;
        if (date == DateTime.Today)
            foreach (var id in batch.StudentIds.Distinct()) { notifier.BatchHoliday(id, batchId, batch.BatchName, date, null, true); n++; }
        return (true, null, n);
    }
}
