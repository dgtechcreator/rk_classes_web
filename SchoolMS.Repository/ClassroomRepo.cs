using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>Classrooms master (soft delete). All SQL parameterised.</summary>
public class ClassroomRepo(CommonConnectivity db)
{
    const string SchemaSql = @"
IF OBJECT_ID('dbo.Classrooms', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Classrooms (
        ClassroomId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Classrooms PRIMARY KEY,
        Name        NVARCHAR(80)  NOT NULL,
        Capacity    INT           NULL,
        Location    NVARCHAR(120) NULL,
        IsActive    BIT           NOT NULL CONSTRAINT DF_Classrooms_IsActive DEFAULT 1,
        IsDeleted   BIT           NOT NULL CONSTRAINT DF_Classrooms_IsDeleted DEFAULT 0,
        CreatedBy   INT           NULL,
        CreatedAt   DATETIME      NOT NULL CONSTRAINT DF_Classrooms_CreatedAt DEFAULT GETDATE(),
        DeletedBy   INT           NULL,
        DeletedAt   DATETIME      NULL
    );
END;
IF COL_LENGTH('dbo.Lectures', 'ClassroomId') IS NULL ALTER TABLE dbo.Lectures ADD ClassroomId INT NULL;";

    public static void CreateSchema(CommonConnectivity db) => db.ExecSql(SchemaSql, new());

    static Classroom Map(Microsoft.Data.SqlClient.SqlDataReader r) => new() {
        ClassroomId = G.G<int>(r, "ClassroomId"), Name = G.G<string>(r, "Name") ?? "", Capacity = G.G<int?>(r, "Capacity"),
        Location = G.G<string>(r, "Location"), IsActive = G.G<bool>(r, "IsActive"),
    };

    public List<Classroom> GetAll(bool includeInactive)
        => db.SqlP("SELECT ClassroomId, Name, Capacity, Location, IsActive FROM Classrooms WHERE IsDeleted = 0 AND (@All = 1 OR IsActive = 1) ORDER BY Name",
            new() { { "@All", includeInactive ? 1 : 0 } }, Map);

    public Classroom? GetById(int id)
        => db.SqlP("SELECT ClassroomId, Name, Capacity, Location, IsActive FROM Classrooms WHERE IsDeleted = 0 AND ClassroomId = @Id", new() { { "@Id", id } }, Map).FirstOrDefault();

    public bool NameExists(string name, int exceptId)
        => db.SqlP("SELECT COUNT(*) AS N FROM Classrooms WHERE IsDeleted = 0 AND LTRIM(RTRIM(Name)) = @N AND ClassroomId <> @Id",
            new() { { "@N", name.Trim() }, { "@Id", exceptId } }, r => G.G<int>(r, "N")).FirstOrDefault() > 0;

    public int Save(Classroom c, int by)
    {
        if (c.ClassroomId > 0)
        {
            db.ExecSql("UPDATE Classrooms SET Name=@Name, Capacity=@Cap, Location=@Loc, IsActive=@Act WHERE ClassroomId=@Id AND IsDeleted=0",
                new() { { "@Id", c.ClassroomId }, { "@Name", c.Name.Trim() }, { "@Cap", c.Capacity }, { "@Loc", string.IsNullOrWhiteSpace(c.Location) ? null : c.Location.Trim() }, { "@Act", c.IsActive } });
            return c.ClassroomId;
        }
        return db.SqlP(@"INSERT INTO Classrooms (Name, Capacity, Location, IsActive, CreatedBy) OUTPUT INSERTED.ClassroomId VALUES (@Name, @Cap, @Loc, @Act, @By)",
            new() { { "@Name", c.Name.Trim() }, { "@Cap", c.Capacity }, { "@Loc", string.IsNullOrWhiteSpace(c.Location) ? null : c.Location.Trim() }, { "@Act", c.IsActive }, { "@By", by } },
            r => G.G<int>(r, "ClassroomId")).FirstOrDefault();
    }

    public void SoftDelete(int id, int by)
        => db.ExecSql("UPDATE Classrooms SET IsDeleted=1, DeletedBy=@By, DeletedAt=GETDATE() WHERE ClassroomId=@Id AND IsDeleted=0", new() { { "@Id", id }, { "@By", by } });

    /// <summary>Lectures still to come (today onwards, not cancelled) that use this room.</summary>
    public int UpcomingLectureCount(int id)
        => db.SqlP("SELECT COUNT(*) AS N FROM Lectures WHERE ClassroomId = @Id AND IsDeleted = 0 AND Status <> 'Cancelled' AND LectureDate >= CAST(GETDATE() AS DATE)",
            new() { { "@Id", id } }, r => G.G<int>(r, "N")).FirstOrDefault();
}
