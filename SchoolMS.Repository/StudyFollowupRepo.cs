using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>Study follow-ups (parent calls + oral tests; soft delete). All SQL parameterised; the table and the "Study Follow-up" right are created on first use (same as SQL/82).</summary>
public class StudyFollowupRepo(CommonConnectivity db)
{
    static volatile bool _ready;
    static readonly object _lock = new();

    const string SchemaSql = @"
IF OBJECT_ID('dbo.StudyFollowups', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudyFollowups (
        FollowupId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudyFollowups PRIMARY KEY,
        StudentId      INT            NOT NULL,
        FollowupAt     DATETIME       NOT NULL CONSTRAINT DF_StudyFollowups_At DEFAULT GETDATE(),
        FollowedBy     INT            NULL,
        Kind           NVARCHAR(10)   NOT NULL CONSTRAINT DF_StudyFollowups_Kind DEFAULT 'Call',
        Mode           NVARCHAR(20)   NULL,
        SpokeTo        NVARCHAR(30)   NULL,
        Category       NVARCHAR(40)   NULL,
        Outcome        NVARCHAR(60)   NULL,
        Subject        NVARCHAR(100)  NULL,
        Topic          NVARCHAR(200)  NULL,
        Result         NVARCHAR(30)   NULL,
        Marks          DECIMAL(6,2)   NULL,
        OutOf          DECIMAL(6,2)   NULL,
        ParentSaid     NVARCHAR(1000) NULL,
        Notes          NVARCHAR(1500) NULL,
        NextDate       DATE           NULL,
        ParentNotified BIT            NOT NULL CONSTRAINT DF_StudyFollowups_Notified DEFAULT 0,
        NotifiedAt     DATETIME       NULL,
        CreatedAt      DATETIME       NOT NULL CONSTRAINT DF_StudyFollowups_Created DEFAULT GETDATE(),
        IsDeleted      BIT            NOT NULL CONSTRAINT DF_StudyFollowups_Del DEFAULT 0,
        DeletedBy      INT            NULL,
        DeletedAt      DATETIME       NULL
    );
    CREATE INDEX IX_StudyFollowups_Student ON dbo.StudyFollowups (StudentId, FollowupAt DESC);
    CREATE INDEX IX_StudyFollowups_At      ON dbo.StudyFollowups (FollowupAt DESC);
END;
IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleKey = 'study_followup')
    INSERT INTO Modules (ModuleName, ModuleKey, Icon, GroupName, OrderNo, IsActive)
    VALUES ('Study Follow-up', 'study_followup', 'fas fa-book-reader', 'Academics', 72, 1);";

    void Ensure()
    {
        if (_ready) return;
        lock (_lock) { if (_ready) return; db.ExecSql(SchemaSql, new()); _ready = true; }
    }

    public void EnsureSchema() => Ensure();

    const string SelectSql = @"
        SELECT f.FollowupId, f.StudentId, f.FollowupAt, f.FollowedBy, u.FullName AS FollowedByName, f.Kind, f.Mode, f.SpokeTo, f.Category, f.Outcome,
               f.Subject, f.Topic, f.Result, f.Marks, f.OutOf, f.ParentSaid, f.Notes, f.NextDate, f.ParentNotified, f.NotifiedAt, f.CreatedAt,
               s.FullName AS StudentName, s.FatherName, s.MotherName, s.AdmissionNo, c.ClassName, sec.SectionName, b.BatchName
        FROM StudyFollowups f
        LEFT JOIN Users    u   ON u.UserId = f.FollowedBy
        LEFT JOIN Students s   ON s.StudentId = f.StudentId
        LEFT JOIN Classes  c   ON c.ClassId = s.ClassId
        LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
        LEFT JOIN Batches  b   ON b.BatchId = s.BatchId
        WHERE f.IsDeleted = 0";

    static StudyFollowup Map(Microsoft.Data.SqlClient.SqlDataReader r) => new() {
        FollowupId = G.G<int>(r, "FollowupId"), StudentId = G.G<int>(r, "StudentId"), FollowupAt = G.G<DateTime>(r, "FollowupAt"),
        FollowedBy = G.G<int?>(r, "FollowedBy"), FollowedByName = G.G<string>(r, "FollowedByName") ?? "",
        Kind = G.G<string>(r, "Kind") ?? StudyRules.Call, Mode = G.G<string>(r, "Mode"), SpokeTo = G.G<string>(r, "SpokeTo"),
        Category = G.G<string>(r, "Category"), Outcome = G.G<string>(r, "Outcome"),
        Subject = G.G<string>(r, "Subject"), Topic = G.G<string>(r, "Topic"), Result = G.G<string>(r, "Result"),
        Marks = G.G<decimal?>(r, "Marks"), OutOf = G.G<decimal?>(r, "OutOf"),
        ParentSaid = G.G<string>(r, "ParentSaid"), Notes = G.G<string>(r, "Notes"), NextDate = G.G<DateTime?>(r, "NextDate"),
        ParentNotified = G.G<bool>(r, "ParentNotified"), NotifiedAt = G.G<DateTime?>(r, "NotifiedAt"), CreatedAt = G.G<DateTime>(r, "CreatedAt"),
        StudentName = StudentNameFormatter.Format(G.G<string>(r, "StudentName"), G.G<string>(r, "FatherName"), G.G<string>(r, "MotherName")),
        AdmissionNo = G.G<string>(r, "AdmissionNo"),
        ClassName = G.G<string>(r, "ClassName"), SectionName = G.G<string>(r, "SectionName"), BatchName = G.G<string>(r, "BatchName"),
    };

    static string? Clean(string? s, int max) { s = s?.Trim(); return string.IsNullOrEmpty(s) ? null : s.Length <= max ? s : s[..max]; }

    public int Add(StudyFollowup f, int by)
    {
        Ensure();
        return db.SqlP(@"INSERT INTO StudyFollowups (StudentId, FollowupAt, FollowedBy, Kind, Mode, SpokeTo, Category, Outcome, Subject, Topic, Result, Marks, OutOf, ParentSaid, Notes, NextDate)
                         OUTPUT INSERTED.FollowupId
                         VALUES (@S, @At, @By, @Kind, @Mode, @Spoke, @Cat, @Out, @Subj, @Topic, @Res, @Marks, @OutOf, @PSaid, @Notes, @Next)",
            new() {
                { "@S", f.StudentId }, { "@At", f.FollowupAt }, { "@By", by }, { "@Kind", f.Kind },
                { "@Mode", Clean(f.Mode, 20) }, { "@Spoke", Clean(f.SpokeTo, 30) }, { "@Cat", Clean(f.Category, 40) }, { "@Out", Clean(f.Outcome, 60) },
                { "@Subj", Clean(f.Subject, 100) }, { "@Topic", Clean(f.Topic, 200) }, { "@Res", Clean(f.Result, 30) },
                { "@Marks", f.Marks }, { "@OutOf", f.OutOf },
                { "@PSaid", Clean(f.ParentSaid, 1000) }, { "@Notes", Clean(f.Notes, 1500) }, { "@Next", f.NextDate?.Date },
            }, r => G.G<int>(r, "FollowupId")).FirstOrDefault();
    }

    public void MarkNotified(int id)
    {
        Ensure();
        db.ExecSql("UPDATE StudyFollowups SET ParentNotified = 1, NotifiedAt = GETDATE() WHERE FollowupId = @Id", new() { { "@Id", id } });
    }

    public StudyFollowup? GetById(int id)
    {
        Ensure();
        return db.SqlP(SelectSql + " AND f.FollowupId = @Id", new() { { "@Id", id } }, Map).FirstOrDefault();
    }

    public void SoftDelete(int id, int by)
    {
        Ensure();
        db.ExecSql("UPDATE StudyFollowups SET IsDeleted = 1, DeletedBy = @By, DeletedAt = GETDATE() WHERE FollowupId = @Id AND IsDeleted = 0", new() { { "@Id", id }, { "@By", by } });
    }

    /// <summary>Newest first. Every filter is optional. [to] is a whole day (included).</summary>
    public List<StudyFollowup> Query(int? studentId, DateTime? from, DateTime? to, int? followedBy, string? kind)
    {
        Ensure();
        return db.SqlP(SelectSql + @"
              AND (@S IS NULL OR f.StudentId = @S)
              AND (@From IS NULL OR f.FollowupAt >= @From)
              AND (@To IS NULL OR f.FollowupAt < DATEADD(day, 1, @To))
              AND (@By IS NULL OR f.FollowedBy = @By)
              AND (@Kind IS NULL OR f.Kind = @Kind)
            ORDER BY f.FollowupAt DESC, f.FollowupId DESC",
            new() { { "@S", studentId }, { "@From", from?.Date }, { "@To", to?.Date }, { "@By", followedBy }, { "@Kind", string.IsNullOrWhiteSpace(kind) ? null : kind } }, Map);
    }

    /// <summary>The student's own details + phone numbers (the follow-up page header).</summary>
    public StudyStudentCard? GetCard(int studentId)
        => db.SqlP(@"
            SELECT s.StudentId, s.FullName, s.FatherName, s.MotherName, s.AdmissionNo,
                   ISNULL(s.Phone,'') AS Phone, ISNULL(s.FatherPhone,'') AS FatherPhone, ISNULL(s.MotherPhone,'') AS MotherPhone,
                   LTRIM(RTRIM(ISNULL(c.ClassName,''))) AS ClassName, LTRIM(RTRIM(ISNULL(sec.SectionName,''))) AS SectionName, LTRIM(RTRIM(ISNULL(b.BatchName,''))) AS BatchName
            FROM Students s
            LEFT JOIN Classes  c   ON c.ClassId = s.ClassId
            LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
            LEFT JOIN Batches  b   ON b.BatchId = s.BatchId
            WHERE s.StudentId = @S",
            new() { { "@S", studentId } },
            r => new StudyStudentCard {
                StudentId = G.G<int>(r, "StudentId"),
                FullName = StudentNameFormatter.Format(G.G<string>(r, "FullName"), G.G<string>(r, "FatherName"), G.G<string>(r, "MotherName")),
                AdmissionNo = G.G<string>(r, "AdmissionNo") ?? "",
                Phone = G.G<string>(r, "Phone") ?? "", FatherPhone = G.G<string>(r, "FatherPhone") ?? "", MotherPhone = G.G<string>(r, "MotherPhone") ?? "",
                ClassName = G.G<string>(r, "ClassName") ?? "", SectionName = G.G<string>(r, "SectionName") ?? "", BatchName = G.G<string>(r, "BatchName") ?? "",
            }).FirstOrDefault();

    /// <summary>Phones of many students at once (the "by student" summary).</summary>
    public Dictionary<int, StudyStudentCard> GetCards(IEnumerable<int> studentIds)
    {
        var ids = studentIds.Distinct().ToList();
        var map = new Dictionary<int, StudyStudentCard>();
        for (int i = 0; i < ids.Count; i += 400)
        {
            var chunk = ids.Skip(i).Take(400).ToList();
            var p = new Dictionary<string, object?>();
            var names = new List<string>();
            for (int k = 0; k < chunk.Count; k++) { var n = "@s" + k; p[n] = chunk[k]; names.Add(n); }
            foreach (var c in db.SqlP($@"
                SELECT s.StudentId, ISNULL(s.Phone,'') AS Phone, ISNULL(s.FatherPhone,'') AS FatherPhone, ISNULL(s.MotherPhone,'') AS MotherPhone
                FROM Students s WHERE s.StudentId IN ({string.Join(",", names)})", p,
                r => new StudyStudentCard { StudentId = G.G<int>(r, "StudentId"), Phone = G.G<string>(r, "Phone") ?? "", FatherPhone = G.G<string>(r, "FatherPhone") ?? "", MotherPhone = G.G<string>(r, "MotherPhone") ?? "" }))
                map[c.StudentId] = c;
        }
        return map;
    }

    /// <summary>The subjects of the institute (for the subject box of an oral test).</summary>
    public List<string> GetSubjectNames()
        => db.Sql("SELECT DISTINCT LTRIM(RTRIM(SubjectName)) AS SubjectName FROM Subjects WHERE IsActive = 1 AND SubjectName IS NOT NULL ORDER BY 1",
                  r => G.G<string>(r, "SubjectName") ?? "").Where(s => s != "").ToList();
}
