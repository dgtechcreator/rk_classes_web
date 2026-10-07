using Microsoft.Data.SqlClient;
using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>All SQL for the Lectures table — fully parameterised, soft-delete only.</summary>
public class LectureRepo(CommonConnectivity db)
{
    private const string SelectSql = @"
        SELECT l.LectureId, l.LectureDate, l.StartTime, l.EndTime,
               l.ClassId, c.ClassName, l.SectionId, s.SectionName, l.BatchId, b.BatchName,
               l.SubjectName, l.FacultyId, f.FullName AS FacultyName,
               l.Topic, l.Remarks, l.Status, l.StatusNote, l.SeriesId
        FROM Lectures l
        LEFT JOIN Classes  c ON c.ClassId   = l.ClassId
        LEFT JOIN Sections s ON s.SectionId = l.SectionId
        LEFT JOIN Batches  b ON b.BatchId   = l.BatchId
        LEFT JOIN Faculty  f ON f.FacultyId = l.FacultyId
        WHERE l.IsDeleted = 0";

    private static Lecture Map(SqlDataReader r) => new()
    {
        LectureId   = G.G<int>(r, "LectureId"),
        LectureDate = G.G<DateTime>(r, "LectureDate"),
        StartTime   = G.G<TimeSpan>(r, "StartTime"),
        EndTime     = G.G<TimeSpan>(r, "EndTime"),
        ClassId     = G.G<int>(r, "ClassId"),
        ClassName   = G.G<string>(r, "ClassName"),
        SectionId   = G.G<int?>(r, "SectionId"),
        SectionName = G.G<string>(r, "SectionName"),
        BatchId     = G.G<int?>(r, "BatchId"),
        BatchName   = G.G<string>(r, "BatchName"),
        SubjectName = G.G<string>(r, "SubjectName") ?? "",
        FacultyId   = G.G<int>(r, "FacultyId"),
        FacultyName = G.G<string>(r, "FacultyName"),
        Topic       = G.G<string>(r, "Topic"),
        Remarks     = G.G<string>(r, "Remarks"),
        Status      = G.G<string>(r, "Status") ?? LectureStatus.Scheduled,
        StatusNote  = G.G<string>(r, "StatusNote"),
        SeriesId    = G.G<Guid?>(r, "SeriesId"),
    };

    private static void P(SqlCommand cmd, string name, object? v) => cmd.Parameters.AddWithValue(name, v ?? DBNull.Value);

    private List<Lecture> Query(string sql, Action<SqlCommand> bind)
    {
        var list = new List<Lecture>();
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c) { CommandTimeout = 60 };
        bind(cmd);
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(Map(r));
        return list;
    }

    public List<Lecture> Search(LectureFilter f)
    {
        var sql = SelectSql + @"
          AND (@From IS NULL OR l.LectureDate >= @From)
          AND (@To   IS NULL OR l.LectureDate <= @To)
          AND (@ClassId   IS NULL OR l.ClassId   = @ClassId)
          AND (@SectionId IS NULL OR l.SectionId = @SectionId)
          AND (@BatchId   IS NULL OR l.BatchId   = @BatchId)
          AND (@Subject   IS NULL OR LTRIM(RTRIM(l.SubjectName)) = @Subject)
          AND (@FacultyId IS NULL OR l.FacultyId = @FacultyId)
          AND (@Status    IS NULL OR l.Status    = @Status)
          AND (@SClass IS NULL OR (l.ClassId = @SClass
                 AND (l.SectionId IS NULL OR l.SectionId = @SSection)
                 AND (l.BatchId   IS NULL OR l.BatchId   = @SBatch)))
        ORDER BY l.LectureDate, l.StartTime, c.ClassName";
        return Query(sql, cmd =>
        {
            P(cmd, "@From", f.From?.Date); P(cmd, "@To", f.To?.Date);
            P(cmd, "@ClassId", f.ClassId); P(cmd, "@SectionId", f.SectionId); P(cmd, "@BatchId", f.BatchId);
            P(cmd, "@Subject", string.IsNullOrWhiteSpace(f.Subject) ? null : f.Subject.Trim());
            P(cmd, "@FacultyId", f.FacultyId); P(cmd, "@Status", string.IsNullOrWhiteSpace(f.Status) ? null : f.Status);
            P(cmd, "@SClass", f.ForStudentClassId); P(cmd, "@SSection", f.ForStudentSectionId); P(cmd, "@SBatch", f.ForStudentBatchId);
        });
    }

    public Lecture? GetById(int id)
        => Query(SelectSql + " AND l.LectureId = @Id", cmd => P(cmd, "@Id", id)).FirstOrDefault();

    public List<Lecture> GetSeries(Guid seriesId, DateTime fromDate)
        => Query(SelectSql + " AND l.SeriesId = @S AND l.LectureDate >= @D ORDER BY l.LectureDate",
            cmd => { P(cmd, "@S", seriesId); P(cmd, "@D", fromDate.Date); });

    private const string InsertSql = @"
        INSERT INTO Lectures (LectureDate, StartTime, EndTime, ClassId, SectionId, BatchId, SubjectName, FacultyId,
                              Topic, Remarks, Status, SeriesId, CreatedBy, CreatedAt)
        VALUES (@Date, @Start, @End, @ClassId, @SectionId, @BatchId, @Subject, @FacultyId,
                @Topic, @Remarks, 'Scheduled', @Series, @User, GETDATE())";

    /// <summary>Inserts every lecture in one transaction — all or nothing.</summary>
    public void InsertMany(List<Lecture> lectures, int userId)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        foreach (var l in lectures)
        {
            using var cmd = new SqlCommand(InsertSql, c, tx);
            P(cmd, "@Date", l.LectureDate.Date); P(cmd, "@Start", l.StartTime); P(cmd, "@End", l.EndTime);
            P(cmd, "@ClassId", l.ClassId); P(cmd, "@SectionId", l.SectionId); P(cmd, "@BatchId", l.BatchId);
            P(cmd, "@Subject", l.SubjectName); P(cmd, "@FacultyId", l.FacultyId);
            P(cmd, "@Topic", l.Topic); P(cmd, "@Remarks", l.Remarks); P(cmd, "@Series", l.SeriesId); P(cmd, "@User", userId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public int Update(Lecture l, int userId)
    {
        const string sql = @"
            UPDATE Lectures SET LectureDate=@Date, StartTime=@Start, EndTime=@End, ClassId=@ClassId, SectionId=@SectionId,
                   BatchId=@BatchId, SubjectName=@Subject, FacultyId=@FacultyId, Topic=@Topic, Remarks=@Remarks,
                   UpdatedBy=@User, UpdatedAt=GETDATE()
            WHERE LectureId=@Id AND IsDeleted=0";
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        P(cmd, "@Id", l.LectureId); P(cmd, "@Date", l.LectureDate.Date); P(cmd, "@Start", l.StartTime); P(cmd, "@End", l.EndTime);
        P(cmd, "@ClassId", l.ClassId); P(cmd, "@SectionId", l.SectionId); P(cmd, "@BatchId", l.BatchId);
        P(cmd, "@Subject", l.SubjectName); P(cmd, "@FacultyId", l.FacultyId);
        P(cmd, "@Topic", l.Topic); P(cmd, "@Remarks", l.Remarks); P(cmd, "@User", userId);
        return cmd.ExecuteNonQuery();
    }

    public int SetStatus(int id, string status, string? note, int userId)
    {
        const string sql = @"UPDATE Lectures SET Status=@S, StatusNote=@N, UpdatedBy=@U, UpdatedAt=GETDATE()
                             WHERE LectureId=@Id AND IsDeleted=0";
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        P(cmd, "@Id", id); P(cmd, "@S", status); P(cmd, "@N", string.IsNullOrWhiteSpace(note) ? null : note.Trim()); P(cmd, "@U", userId);
        return cmd.ExecuteNonQuery();
    }

    public int SoftDelete(int id, int userId)
    {
        const string sql = "UPDATE Lectures SET IsDeleted=1, DeletedBy=@U, DeletedAt=GETDATE() WHERE LectureId=@Id AND IsDeleted=0";
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        P(cmd, "@Id", id); P(cmd, "@U", userId);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>Soft-deletes the not-yet-completed lectures of a recurring series from a date onward.</summary>
    public int SoftDeleteSeries(Guid seriesId, DateTime fromDate, int userId)
    {
        const string sql = @"UPDATE Lectures SET IsDeleted=1, DeletedBy=@U, DeletedAt=GETDATE()
                             WHERE SeriesId=@S AND LectureDate>=@D AND IsDeleted=0 AND Status <> 'Completed'";
        using var c = db.Open();
        using var cmd = new SqlCommand(sql, c);
        P(cmd, "@S", seriesId); P(cmd, "@D", fromDate.Date); P(cmd, "@U", userId);
        return cmd.ExecuteNonQuery();
    }
}
