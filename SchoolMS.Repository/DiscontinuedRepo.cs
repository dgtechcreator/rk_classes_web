using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>
/// SQL for DiscontinuedStudents (students who left / were removed, with the reason and a fee snapshot). The table and its
/// permission module are created on first start (same as SQL/76), so a deploy that forgot the script still works.
/// </summary>
public class DiscontinuedRepo(CommonConnectivity db)
{
    const string SchemaSql = @"
IF OBJECT_ID('dbo.DiscontinuedStudents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiscontinuedStudents (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscontinuedStudents PRIMARY KEY,
        StudentId      INT            NOT NULL,
        Kind           NVARCHAR(20)   NOT NULL,
        Reason         NVARCHAR(1000) NOT NULL,
        LeftOn         DATE           NOT NULL,
        ClassName      NVARCHAR(100)  NULL,
        SectionName    NVARCHAR(100)  NULL,
        BatchName      NVARCHAR(100)  NULL,
        TotalFees      DECIMAL(18,2)  NOT NULL DEFAULT 0,
        Discount       DECIMAL(18,2)  NOT NULL DEFAULT 0,
        Paid           DECIMAL(18,2)  NOT NULL DEFAULT 0,
        Pending        DECIMAL(18,2)  NOT NULL DEFAULT 0,
        AttendancePct  DECIMAL(9,2)   NOT NULL DEFAULT 0,
        MarksPct       DECIMAL(9,2)   NOT NULL DEFAULT 0,
        RecordedBy     INT            NULL,
        RecordedByName NVARCHAR(150)  NULL,
        RecordedAt     DATETIME       NOT NULL CONSTRAINT DF_DiscontinuedStudents_At DEFAULT GETDATE()
    );
    CREATE INDEX IX_DiscontinuedStudents_Student ON dbo.DiscontinuedStudents (StudentId);
END;
IF COL_LENGTH('dbo.DiscontinuedStudents', 'RevertedAt') IS NULL
    ALTER TABLE dbo.DiscontinuedStudents ADD RevertedAt DATETIME NULL, RevertedBy INT NULL, RevertedByName NVARCHAR(150) NULL, RevertNote NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'discontinued_students')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Discontinued Students', 'discontinued_students', 'fas fa-user-slash', 'Students', 32, 1);";

    static volatile bool _ready;
    static readonly object _lock = new();

    public void EnsureSchema()
    {
        if (_ready) return;
        lock (_lock)
        {
            if (_ready) return;
            db.ExecSql(SchemaSql, new());
            _ready = true;
        }
    }

    public void Insert(DiscontinuedStudent d)
    {
        EnsureSchema();
        db.ExecSql(@"
            INSERT INTO DiscontinuedStudents
              (StudentId, Kind, Reason, LeftOn, ClassName, SectionName, BatchName, TotalFees, Discount, Paid, Pending,
               AttendancePct, MarksPct, RecordedBy, RecordedByName)
            VALUES (@S, @K, @R, @On, @C, @Sec, @B, @T, @D, @P, @Pe, @A, @M, @By, @ByN)",
            new() {
                { "@S", d.StudentId }, { "@K", d.Kind }, { "@R", d.Reason }, { "@On", d.LeftOn.Date },
                { "@C", d.ClassName }, { "@Sec", d.SectionName }, { "@B", d.BatchName },
                { "@T", d.TotalFees }, { "@D", d.Discount }, { "@P", d.Paid }, { "@Pe", d.Pending },
                { "@A", d.AttendancePct }, { "@M", d.MarksPct },
                { "@By", d.RecordedBy > 0 ? d.RecordedBy : null }, { "@ByN", d.RecordedByName },
            });
    }

    /// <summary>Marks the entry as reverted (the student is made Active by the caller). The record itself is kept.</summary>
    public void MarkReverted(int id, int by, string byName, string? note)
    {
        EnsureSchema();
        db.ExecSql("UPDATE DiscontinuedStudents SET RevertedAt=GETDATE(), RevertedBy=@By, RevertedByName=@N, RevertNote=@Note WHERE Id=@Id AND RevertedAt IS NULL",
            new() { { "@Id", id }, { "@By", by > 0 ? by : null }, { "@N", byName }, { "@Note", string.IsNullOrWhiteSpace(note) ? null : note.Trim() } });
    }

    /// <summary>Newest first. Student columns come from the live Students row (name can still be corrected later).</summary>
    public List<DiscontinuedStudent> GetAll()
    {
        EnsureSchema();
        var all = db.SqlP(@"
            SELECT d.*, s.FullName, s.FatherName, s.MotherName, s.AdmissionNo, s.FatherPhone, s.Status AS CurrentStatus
            FROM DiscontinuedStudents d
            LEFT JOIN Students s ON s.StudentId = d.StudentId
            ORDER BY d.LeftOn DESC, d.Id DESC", new(),
            r => new DiscontinuedStudent {
                Id = G.G<int>(r, "Id"), StudentId = G.G<int>(r, "StudentId"),
                StudentName = r.StudentName(db) ?? "", AdmissionNo = G.G<string>(r, "AdmissionNo") ?? "",
                ClassName = G.G<string>(r, "ClassName") ?? "", SectionName = G.G<string>(r, "SectionName") ?? "", BatchName = G.G<string>(r, "BatchName") ?? "",
                FatherPhone = G.G<string>(r, "FatherPhone") ?? "",
                Kind = G.G<string>(r, "Kind") ?? DiscontinueKind.Left, Reason = G.G<string>(r, "Reason") ?? "",
                LeftOn = G.G<DateTime>(r, "LeftOn"),
                TotalFees = G.G<decimal>(r, "TotalFees"), Discount = G.G<decimal>(r, "Discount"),
                Paid = G.G<decimal>(r, "Paid"), Pending = G.G<decimal>(r, "Pending"),
                AttendancePct = G.G<decimal>(r, "AttendancePct"), MarksPct = G.G<decimal>(r, "MarksPct"),
                RecordedBy = G.G<int?>(r, "RecordedBy") ?? 0, RecordedByName = G.G<string>(r, "RecordedByName") ?? "",
                RecordedAt = G.G<DateTime>(r, "RecordedAt"), CurrentStatus = G.G<string>(r, "CurrentStatus") ?? "Inactive",
                RevertedAt = G.G<DateTime?>(r, "RevertedAt"), RevertedByName = G.G<string>(r, "RevertedByName") ?? "", RevertNote = G.G<string>(r, "RevertNote") ?? "",
            });
        // Only the newest entry of a student that is still discontinued can be reverted.
        var newest = all.GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => g.Max(x => x.Id));
        foreach (var d in all) d.CanRevert = !d.Returned && newest[d.StudentId] == d.Id;
        return all;
    }
}
