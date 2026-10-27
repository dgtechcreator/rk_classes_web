using Microsoft.Data.SqlClient;
using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>All SQL for ClassNotes (class notes + daily practice papers). Parameterised, soft-delete only. The table is created on first use.</summary>
public class ClassNotesRepo(CommonConnectivity db)
{
    static volatile bool _ready;
    static readonly object _lock = new();

    const string SchemaSql = @"
IF OBJECT_ID('dbo.ClassNotes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ClassNotes (
        NoteId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClassNotes PRIMARY KEY,
        Kind          NVARCHAR(20)  NOT NULL,
        Title         NVARCHAR(200) NOT NULL,
        Description   NVARCHAR(1000) NULL,
        ClassId       INT NOT NULL,
        SectionId     INT NULL,
        BatchId       INT NULL,
        SubjectName   NVARCHAR(100) NULL,
        FileName      NVARCHAR(260) NOT NULL,
        StoredName    NVARCHAR(100) NOT NULL,
        FileSize      BIGINT NOT NULL DEFAULT 0,
        PaperDate     DATE NOT NULL,
        CreatedBy     INT NULL,
        CreatedByName NVARCHAR(150) NULL,
        CreatedAt     DATETIME NOT NULL CONSTRAINT DF_ClassNotes_CreatedAt DEFAULT GETDATE(),
        IsDeleted     BIT NOT NULL CONSTRAINT DF_ClassNotes_IsDeleted DEFAULT 0
    );
    CREATE INDEX IX_ClassNotes_Class ON dbo.ClassNotes (ClassId, IsDeleted, PaperDate DESC);
END;
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'class_notes')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Notes & Practice Papers', 'class_notes', 'fas fa-file-lines', 'Academics', 70, 1);";

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

    const string SelectSql = @"
        SELECT n.NoteId, n.Kind, n.Title, n.Description, n.ClassId, c.ClassName, n.SectionId, s.SectionName, n.BatchId, b.BatchName,
               n.SubjectName, n.FileName, n.StoredName, n.FileSize, n.PaperDate, n.CreatedBy, n.CreatedByName, n.CreatedAt
        FROM ClassNotes n
        LEFT JOIN Classes  c ON c.ClassId   = n.ClassId
        LEFT JOIN Sections s ON s.SectionId = n.SectionId
        LEFT JOIN Batches  b ON b.BatchId   = n.BatchId
        WHERE n.IsDeleted = 0";

    static ClassNote Map(SqlDataReader r) => new()
    {
        NoteId = G.G<int>(r, "NoteId"), Kind = G.G<string>(r, "Kind") ?? NoteKind.Notes, Title = G.G<string>(r, "Title") ?? "",
        Description = G.G<string>(r, "Description"), ClassId = G.G<int>(r, "ClassId"), ClassName = G.G<string>(r, "ClassName"),
        SectionId = G.G<int?>(r, "SectionId"), SectionName = G.G<string>(r, "SectionName"),
        BatchId = G.G<int?>(r, "BatchId"), BatchName = G.G<string>(r, "BatchName"), SubjectName = G.G<string>(r, "SubjectName"),
        FileName = G.G<string>(r, "FileName") ?? "", StoredName = G.G<string>(r, "StoredName") ?? "", FileSize = G.G<long>(r, "FileSize"),
        PaperDate = G.G<DateTime>(r, "PaperDate"), CreatedBy = G.G<int?>(r, "CreatedBy"), CreatedByName = G.G<string>(r, "CreatedByName"),
        CreatedAt = G.G<DateTime>(r, "CreatedAt"),
    };

    public int Insert(ClassNote n)
    {
        EnsureSchema();
        var id = db.SqlP(@"
            INSERT INTO ClassNotes (Kind, Title, Description, ClassId, SectionId, BatchId, SubjectName, FileName, StoredName, FileSize, PaperDate, CreatedBy, CreatedByName)
            VALUES (@Kind, @Title, @Desc, @C, @Sec, @Bat, @Sub, @FN, @SN, @Size, @Date, @By, @ByName);
            SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id",
            new() {
                { "@Kind", n.Kind }, { "@Title", n.Title }, { "@Desc", n.Description }, { "@C", n.ClassId }, { "@Sec", n.SectionId }, { "@Bat", n.BatchId },
                { "@Sub", n.SubjectName }, { "@FN", n.FileName }, { "@SN", n.StoredName }, { "@Size", n.FileSize }, { "@Date", n.PaperDate.Date },
                { "@By", n.CreatedBy }, { "@ByName", n.CreatedByName },
            }, r => G.G<int>(r, "Id")).FirstOrDefault();
        return id;
    }

    public List<ClassNote> Search(ClassNoteFilter f, int take = 300)
    {
        EnsureSchema();
        return db.SqlP($@"{SelectSql}
            AND (@C IS NULL OR n.ClassId = @C)
            AND (@Sec IS NULL OR n.SectionId = @Sec)
            AND (@Bat IS NULL OR n.BatchId = @Bat)
            AND (@Kind IS NULL OR n.Kind = @Kind)
            AND (@Sub IS NULL OR LTRIM(RTRIM(n.SubjectName)) = @Sub)
            AND (@SC IS NULL OR (n.ClassId = @SC
                   AND (n.SectionId IS NULL OR n.SectionId = @SS)
                   AND (n.BatchId   IS NULL OR n.BatchId   = @SB)))
            ORDER BY n.PaperDate DESC, n.NoteId DESC
            OFFSET 0 ROWS FETCH NEXT {Math.Clamp(take, 1, 1000)} ROWS ONLY",
            new() {
                { "@C", f.ClassId }, { "@Sec", f.SectionId }, { "@Bat", f.BatchId }, { "@Kind", string.IsNullOrWhiteSpace(f.Kind) ? null : f.Kind },
                { "@Sub", string.IsNullOrWhiteSpace(f.Subject) ? null : f.Subject.Trim() },
                { "@SC", f.ForStudentClassId }, { "@SS", f.ForStudentSectionId }, { "@SB", f.ForStudentBatchId },
            }, Map);
    }

    public ClassNote? GetById(int id)
    {
        EnsureSchema();
        return db.SqlP(SelectSql + " AND n.NoteId = @Id", new() { { "@Id", id } }, Map).FirstOrDefault();
    }

    public bool SoftDelete(int id)
    {
        EnsureSchema();
        return db.ExecSql("UPDATE ClassNotes SET IsDeleted = 1 WHERE NoteId = @Id AND IsDeleted = 0", new() { { "@Id", id } }) > 0;
    }
}
