using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

/// <summary>Class Representatives. Plain parameterised SQL (no stored procedures) - table comes from SQL/61.</summary>
public class ClassRepRepo(CommonConnectivity db)
{
    const string SelectSql = @"
        SELECT cr.CrId, cr.UserId, u.Username, u.IsActive AS UserActive, cr.StudentId,
               s.FullName, s.FatherName, s.MotherName, s.AdmissionNo, s.Status AS StudentStatus,
               s.ClassId, c.ClassName, s.SectionId, sec.SectionName, s.BatchId, b.BatchName,
               cr.LimitToBatch, cr.IsActive, cr.CreatedAt
        FROM ClassRepresentatives cr
        JOIN Users u         ON u.UserId    = cr.UserId
        JOIN Students s      ON s.StudentId = cr.StudentId
        LEFT JOIN Classes c  ON c.ClassId   = s.ClassId
        LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
        LEFT JOIN Batches b  ON b.BatchId   = s.BatchId";

    static ClassRep Map(SqlDataReader r) => new() {
        CrId          = G.G<int>(r, "CrId"),
        UserId        = G.G<int>(r, "UserId"),
        Username      = G.G<string>(r, "Username") ?? "",
        UserActive    = G.G<bool>(r, "UserActive"),
        StudentId     = G.G<int>(r, "StudentId"),
        StudentName   = StudentNameFormatter.Format(G.G<string>(r, "FullName") ?? "", G.G<string>(r, "FatherName"), G.G<string>(r, "MotherName")),
        AdmissionNo   = G.G<string>(r, "AdmissionNo") ?? "",
        StudentStatus = G.G<string>(r, "StudentStatus"),
        ClassId       = G.G<int?>(r, "ClassId"),
        ClassName     = G.G<string>(r, "ClassName"),
        SectionId     = G.G<int?>(r, "SectionId"),
        SectionName   = G.G<string>(r, "SectionName"),
        BatchId       = G.G<int?>(r, "BatchId"),
        BatchName     = G.G<string>(r, "BatchName"),
        LimitToBatch  = G.G<bool>(r, "LimitToBatch"),
        IsActive      = G.G<bool>(r, "IsActive"),
        CreatedAt     = G.G<DateTime>(r, "CreatedAt"),
    };

    public List<ClassRep> GetAll()
        => db.Sql(SelectSql + " ORDER BY cr.IsActive DESC, c.ClassName, s.FullName", Map);

    /// <summary>Module keys held by every CR login (UserId -> keys).</summary>
    public Dictionary<int, List<string>> GetRights()
    {
        var rows = db.Sql(@"SELECT up.UserId, m.ModuleKey
                            FROM UserPermissions up
                            JOIN Modules m ON m.ModuleId = up.ModuleId
                            JOIN ClassRepresentatives cr ON cr.UserId = up.UserId
                            WHERE up.CanView = 1 OR up.CanEdit = 1",
            r => (UserId: G.G<int>(r, "UserId"), Key: G.G<string>(r, "ModuleKey") ?? ""));
        return rows.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());
    }

    public ClassRep? GetById(int crId)
        => db.SqlP(SelectSql + " WHERE cr.CrId=@Id", new() {{"@Id", crId}}, Map).FirstOrDefault();

    public ClassRep? GetByUserId(int userId)
        => db.SqlP(SelectSql + " WHERE cr.UserId=@Id", new() {{"@Id", userId}}, Map).FirstOrDefault();

    public ClassRep? GetByStudentId(int studentId)
        => db.SqlP(SelectSql + " WHERE cr.StudentId=@Id", new() {{"@Id", studentId}}, Map).FirstOrDefault();

    public int Insert(int userId, int studentId, bool limitToBatch, int by)
        => db.SqlP("INSERT INTO ClassRepresentatives(UserId,StudentId,LimitToBatch,IsActive,CreatedBy) OUTPUT INSERTED.CrId VALUES(@U,@S,@L,1,@By)",
            new() {{"@U", userId}, {"@S", studentId}, {"@L", limitToBatch}, {"@By", by}},
            r => G.G<int>(r, "CrId")).FirstOrDefault();

    public void Update(int crId, bool limitToBatch, bool isActive)
        => db.ExecSql("UPDATE ClassRepresentatives SET LimitToBatch=@L, IsActive=@A WHERE CrId=@Id",
            new() {{"@Id", crId}, {"@L", limitToBatch}, {"@A", isActive}});

    /// <summary>RoleId of the 'Class Representative' role (null when SQL/61 has not been run).</summary>
    public int? RoleId()
    {
        var l = db.Sql("SELECT RoleId FROM Roles WHERE RoleName='Class Representative'", r => G.G<int>(r, "RoleId"));
        return l.Count > 0 ? l[0] : null;
    }
}
