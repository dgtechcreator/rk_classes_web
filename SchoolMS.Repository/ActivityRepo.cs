using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>
/// All SQL for StudentActivities (what happened to a student) and the parent's "seen up to" marker. Fully parameterised.
/// The table is created on first use (same DDL as SQL/71), so a deploy that forgot to run the script still works.
/// </summary>
public class ActivityRepo(CommonConnectivity db)
{
    static volatile bool _schemaReady;
    static readonly object _lock = new();

    const string SchemaSql = @"
IF OBJECT_ID('dbo.StudentActivities', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentActivities (
        ActivityId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudentActivities PRIMARY KEY,
        StudentId   INT            NOT NULL,
        Kind        NVARCHAR(30)   NOT NULL,
        Title       NVARCHAR(200)  NOT NULL,
        [Message]   NVARCHAR(1000) NOT NULL,
        DedupeKey   NVARCHAR(150)  NULL,
        ReferenceId INT            NULL,
        CreatedAt   DATETIME       NOT NULL CONSTRAINT DF_StudentActivities_CreatedAt DEFAULT GETDATE()
    );
    CREATE INDEX IX_StudentActivities_Student ON dbo.StudentActivities (StudentId, CreatedAt DESC);
    CREATE INDEX IX_StudentActivities_Dedupe  ON dbo.StudentActivities (StudentId, DedupeKey);
END;
IF COL_LENGTH('dbo.ParentAccounts', 'NotifSeenAt') IS NULL
    ALTER TABLE dbo.ParentAccounts ADD NotifSeenAt DATETIME NULL;";

    public void EnsureSchema()
    {
        if (_schemaReady) return;
        lock (_lock)
        {
            if (_schemaReady) return;
            db.ExecSql(SchemaSql, new());
            _schemaReady = true;
        }
    }

    // ── writing ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds an activity. With a DedupeKey the same event is one row: saving it again with the same text does nothing,
    /// with changed text (absent -> present, marks corrected) the row is updated and counts as new again.
    /// </summary>
    public void Upsert(int studentId, string kind, string title, string message, string? dedupeKey, int? referenceId)
    {
        EnsureSchema();
        db.ExecSql(@"
            DECLARE @curId INT, @curMsg NVARCHAR(1000), @curKind NVARCHAR(30);   -- (SQL names are not case-sensitive: must differ from @Msg / @Kind)
            IF @Key IS NOT NULL
                SELECT TOP 1 @curId = ActivityId, @curMsg = [Message], @curKind = Kind
                FROM StudentActivities WHERE StudentId = @S AND DedupeKey = @Key ORDER BY ActivityId DESC;
            IF @curId IS NULL
                INSERT INTO StudentActivities (StudentId, Kind, Title, [Message], DedupeKey, ReferenceId)
                VALUES (@S, @Kind, @Title, @Msg, @Key, @Ref);
            ELSE IF @curMsg <> @Msg OR @curKind <> @Kind
                UPDATE StudentActivities
                SET Kind = @Kind, Title = @Title, [Message] = @Msg, ReferenceId = @Ref, CreatedAt = GETDATE()
                WHERE ActivityId = @curId;",
            new() {
                { "@S", studentId }, { "@Kind", kind }, { "@Title", Cut(title, 200) }, { "@Msg", Cut(message, 1000) },
                { "@Key", dedupeKey == null ? null : Cut(dedupeKey, 150) }, { "@Ref", referenceId },
            });
    }

    /// <summary>One row for every active student of the class (and section / batch when given). Returns how many were added.</summary>
    public int InsertForGroup(int classId, int? sectionId, int? batchId, string kind, string title, string message, string dedupeKey, int? referenceId)
    {
        EnsureSchema();
        return db.ExecSql($@"
            INSERT INTO StudentActivities (StudentId, Kind, Title, [Message], DedupeKey, ReferenceId)
            SELECT s.StudentId, @Kind, @Title, @Msg, @Key, @Ref
            FROM Students s
            WHERE s.ClassId = @C
              AND (@Sec IS NULL OR s.SectionId = @Sec)
              AND (@Bat IS NULL OR s.BatchId = @Bat)
              AND ISNULL(s.Status, 'Active') = 'Active'
              AND (s.AcademicYearId IS NULL OR s.AcademicYearId = {CurrentYear.Id})
              AND NOT EXISTS (SELECT 1 FROM StudentActivities a
                              WHERE a.StudentId = s.StudentId AND a.DedupeKey = @Key AND a.[Message] = @Msg)",
            new() {
                { "@C", classId }, { "@Sec", sectionId }, { "@Bat", batchId }, { "@Kind", kind },
                { "@Title", Cut(title, 200) }, { "@Msg", Cut(message, 1000) }, { "@Key", Cut(dedupeKey, 150) }, { "@Ref", referenceId },
            });
    }

    // ── looking things up for the message text ─────────────────────────────────────────────

    public (string display, string? className)? GetStudentLabel(int studentId)
    {
        var l = db.SqlP(@"
            SELECT s.FullName, s.FatherName, s.MotherName, c.ClassName
            FROM Students s LEFT JOIN Classes c ON c.ClassId = s.ClassId WHERE s.StudentId = @S",
            new() { { "@S", studentId } },
            r => (ShortName(G.G<string>(r, "FullName"), G.G<string>(r, "FatherName"), G.G<string>(r, "MotherName")),
                  G.G<string>(r, "ClassName")));
        return l.Count == 0 ? null : l[0];
    }

    public string? GetSubjectName(int subjectId)
        => db.SqlP("SELECT SubjectName FROM Subjects WHERE SubjectId = @Id", new() { { "@Id", subjectId } }, r => G.G<string>(r, "SubjectName"))
             .FirstOrDefault();

    public (string? cls, string? sec, string? bat, string? faculty) GetLectureLabels(int classId, int? sectionId, int? batchId, int facultyId)
        => db.SqlP(@"
            SELECT (SELECT ClassName   FROM Classes  WHERE ClassId   = @C) AS Cls,
                   (SELECT SectionName FROM Sections WHERE SectionId = @S) AS Sec,
                   (SELECT BatchName   FROM Batches  WHERE BatchId   = @B) AS Bat,
                   (SELECT FullName    FROM Faculty  WHERE FacultyId = @F) AS Fac",
            new() { { "@C", classId }, { "@S", sectionId }, { "@B", batchId }, { "@F", facultyId } },
            r => (G.G<string>(r, "Cls"), G.G<string>(r, "Sec"), G.G<string>(r, "Bat"), G.G<string>(r, "Fac"))).FirstOrDefault();

    public int? GetUserIdByFaculty(int facultyId)
        => db.SqlP("SELECT TOP 1 UserId FROM Users WHERE FacultyId = @F AND ISNULL(IsDeleted, 0) = 0 AND IsActive = 1 ORDER BY UserId",
                   new() { { "@F", facultyId } }, r => (int?)G.G<int>(r, "UserId")).FirstOrDefault();

    // ── parent side ─────────────────────────────────────────────────────────────────────────

    static readonly string ParentScope = @"
        FROM StudentActivities a
        JOIN Students s ON s.StudentId = a.StudentId
        LEFT JOIN Classes c ON c.ClassId = s.ClassId
        WHERE " + PhoneMatch.AnyPhoneOf("s", "@PK") + @"
          AND ISNULL(s.Status, 'Active') = 'Active'";

    DateTime SeenAt(string phone)
        => db.SqlP("SELECT NotifSeenAt FROM ParentAccounts WHERE Phone = @P", new() { { "@P", phone } }, r => G.G<DateTime?>(r, "NotifSeenAt"))
             .FirstOrDefault() ?? new DateTime(2000, 1, 1);

    /// <summary>Newest first. [studentId] limits it to one child.</summary>
    public List<StudentActivity> GetForParent(string phone, int? studentId, int take)
    {
        EnsureSchema();
        var seen = SeenAt(phone);
        return db.SqlP($@"
            SELECT TOP (@Take) a.ActivityId, a.StudentId, a.Kind, a.Title, a.[Message], a.ReferenceId, a.CreatedAt,
                   s.FullName, s.FatherName, s.MotherName, c.ClassName
            {ParentScope}
              AND (@Child IS NULL OR a.StudentId = @Child)
            ORDER BY a.CreatedAt DESC, a.ActivityId DESC",
            new() { { "@PK", PhoneMatch.Key(phone) }, { "@Child", studentId }, { "@Take", Math.Clamp(take, 1, 300) } },
            r =>
            {
                var created = G.G<DateTime>(r, "CreatedAt");
                return new StudentActivity {
                    ActivityId = G.G<int>(r, "ActivityId"), StudentId = G.G<int>(r, "StudentId"),
                    StudentName = ShortName(G.G<string>(r, "FullName"), G.G<string>(r, "FatherName"), G.G<string>(r, "MotherName")),
                    ClassName = G.G<string>(r, "ClassName"),
                    Kind = G.G<string>(r, "Kind") ?? "", Title = G.G<string>(r, "Title") ?? "", Message = G.G<string>(r, "Message") ?? "",
                    ReferenceId = G.G<int?>(r, "ReferenceId"), CreatedAt = created, IsUnread = created > seen,
                };
            });
    }

    public int GetUnreadForParent(string phone)
    {
        EnsureSchema();
        var seen = SeenAt(phone);
        return db.SqlP($"SELECT COUNT(*) AS cnt {ParentScope} AND a.CreatedAt > @Seen",
            new() { { "@PK", PhoneMatch.Key(phone) }, { "@Seen", seen } }, r => G.G<int>(r, "cnt")).FirstOrDefault();
    }

    public void MarkSeen(string phone)
    {
        EnsureSchema();
        db.ExecSql("UPDATE ParentAccounts SET NotifSeenAt = GETDATE() WHERE Phone = @P", new() { { "@P", phone } });
    }

    /// <summary>"Singh Ananya Vijay Kavita" -> "Singh Ananya": the child's own name as the school writes it, without the parents' names.</summary>
    public static string ShortName(string? full, string? father, string? mother)
    {
        var words = StudentNameFormatter.Format(full, father, mother).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Take(2));
    }

    static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
}
