using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>Fee follow-up notes (soft delete). All SQL parameterised; the table is created on first use (same DDL as SQL/80).</summary>
public class FeeFollowupRepo(CommonConnectivity db)
{
    static volatile bool _ready;
    static readonly object _lock = new();

    const string SchemaSql = @"
IF OBJECT_ID('dbo.FeeFollowups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FeeFollowups (
        FollowupId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeeFollowups PRIMARY KEY,
        StudentId      INT            NOT NULL,
        FollowupAt     DATETIME       NOT NULL CONSTRAINT DF_FeeFollowups_At DEFAULT GETDATE(),
        FollowedBy     INT            NULL,
        Mode           NVARCHAR(20)   NOT NULL CONSTRAINT DF_FeeFollowups_Mode DEFAULT 'Call',
        SpokeTo        NVARCHAR(30)   NULL,
        Outcome        NVARCHAR(40)   NOT NULL CONSTRAINT DF_FeeFollowups_Outcome DEFAULT '',
        Reason         NVARCHAR(300)  NULL,
        Notes          NVARCHAR(1500) NULL,
        PromisedAmount DECIMAL(12,2)  NULL,
        NextDate       DATE           NULL,
        NextDateBy     NVARCHAR(10)   NULL,
        BalanceAtTime  DECIMAL(12,2)  NULL,
        CreatedAt      DATETIME       NOT NULL CONSTRAINT DF_FeeFollowups_Created DEFAULT GETDATE(),
        IsDeleted      BIT            NOT NULL CONSTRAINT DF_FeeFollowups_Del DEFAULT 0,
        DeletedBy      INT            NULL,
        DeletedAt      DATETIME       NULL
    );
    CREATE INDEX IX_FeeFollowups_Student ON dbo.FeeFollowups (StudentId, FollowupAt DESC);
    CREATE INDEX IX_FeeFollowups_Next    ON dbo.FeeFollowups (NextDate);
END";

    void Ensure()
    {
        if (_ready) return;
        lock (_lock) { if (_ready) return; db.ExecSql(SchemaSql, new()); _ready = true; }
    }

    public void EnsureSchema() => Ensure();

    const string SelectSql = @"
        SELECT f.FollowupId, f.StudentId, f.FollowupAt, f.FollowedBy, u.FullName AS FollowedByName, f.Mode, f.SpokeTo, f.Outcome, f.Reason, f.Notes,
               f.PromisedAmount, f.NextDate, f.NextDateBy, f.BalanceAtTime, f.CreatedAt,
               s.FullName AS StudentName, s.AdmissionNo, c.ClassName, sec.SectionName, b.BatchName
        FROM FeeFollowups f
        LEFT JOIN Users    u   ON u.UserId = f.FollowedBy
        LEFT JOIN Students s   ON s.StudentId = f.StudentId
        LEFT JOIN Classes  c   ON c.ClassId = s.ClassId
        LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
        LEFT JOIN Batches  b   ON b.BatchId = s.BatchId
        WHERE f.IsDeleted = 0";

    static FeeFollowup Map(Microsoft.Data.SqlClient.SqlDataReader r) => new() {
        FollowupId = G.G<int>(r, "FollowupId"), StudentId = G.G<int>(r, "StudentId"), FollowupAt = G.G<DateTime>(r, "FollowupAt"),
        FollowedBy = G.G<int?>(r, "FollowedBy"), FollowedByName = G.G<string>(r, "FollowedByName") ?? "",
        Mode = G.G<string>(r, "Mode") ?? "Call", SpokeTo = G.G<string>(r, "SpokeTo"), Outcome = G.G<string>(r, "Outcome") ?? "",
        Reason = G.G<string>(r, "Reason"), Notes = G.G<string>(r, "Notes"), PromisedAmount = G.G<decimal?>(r, "PromisedAmount"),
        NextDate = G.G<DateTime?>(r, "NextDate"), NextDateBy = G.G<string>(r, "NextDateBy"), BalanceAtTime = G.G<decimal?>(r, "BalanceAtTime"),
        CreatedAt = G.G<DateTime>(r, "CreatedAt"), StudentName = G.G<string>(r, "StudentName"), AdmissionNo = G.G<string>(r, "AdmissionNo"),
        ClassName = G.G<string>(r, "ClassName"), SectionName = G.G<string>(r, "SectionName"), BatchName = G.G<string>(r, "BatchName"),
    };

    public int Add(FeeFollowup f, int by)
    {
        Ensure();
        return db.SqlP(@"INSERT INTO FeeFollowups (StudentId, FollowupAt, FollowedBy, Mode, SpokeTo, Outcome, Reason, Notes, PromisedAmount, NextDate, NextDateBy, BalanceAtTime)
                         OUTPUT INSERTED.FollowupId
                         VALUES (@S, @At, @By, @Mode, @Spoke, @Out, @Reason, @Notes, @Prom, @Next, @NextBy, @Bal)",
            new() {
                { "@S", f.StudentId }, { "@At", f.FollowupAt }, { "@By", by }, { "@Mode", f.Mode }, { "@Spoke", f.SpokeTo }, { "@Out", f.Outcome },
                { "@Reason", string.IsNullOrWhiteSpace(f.Reason) ? null : f.Reason.Trim() }, { "@Notes", string.IsNullOrWhiteSpace(f.Notes) ? null : f.Notes.Trim() },
                { "@Prom", f.PromisedAmount }, { "@Next", f.NextDate?.Date }, { "@NextBy", f.NextDate == null ? null : f.NextDateBy }, { "@Bal", f.BalanceAtTime },
            }, r => G.G<int>(r, "FollowupId")).FirstOrDefault();
    }

    public FeeFollowup? GetById(int id)
    {
        Ensure();
        return db.SqlP(SelectSql + " AND f.FollowupId = @Id", new() { { "@Id", id } }, Map).FirstOrDefault();
    }

    public void SoftDelete(int id, int by)
    {
        Ensure();
        db.ExecSql("UPDATE FeeFollowups SET IsDeleted = 1, DeletedBy = @By, DeletedAt = GETDATE() WHERE FollowupId = @Id AND IsDeleted = 0", new() { { "@Id", id }, { "@By", by } });
    }

    /// <summary>Newest first. Every filter is optional.</summary>
    public List<FeeFollowup> Query(int? studentId, DateTime? from, DateTime? to, int? followedBy, string? outcome)
    {
        Ensure();
        return db.SqlP(SelectSql + @"
              AND (@S IS NULL OR f.StudentId = @S)
              AND (@From IS NULL OR f.FollowupAt >= @From)
              AND (@To IS NULL OR f.FollowupAt < DATEADD(day, 1, @To))
              AND (@By IS NULL OR f.FollowedBy = @By)
              AND (@Out IS NULL OR f.Outcome = @Out)
            ORDER BY f.FollowupAt DESC, f.FollowupId DESC",
            new() { { "@S", studentId }, { "@From", from?.Date }, { "@To", to?.Date }, { "@By", followedBy }, { "@Out", string.IsNullOrWhiteSpace(outcome) ? null : outcome } }, Map);
    }
}
