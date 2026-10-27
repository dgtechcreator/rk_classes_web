using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class ParentRepo(CommonConnectivity db)
{
    public ParentAccount? GetByPhone(string phone)
    {
        var l = db.Read("sp_ParentGetByPhone", new() { { "@Phone", phone } }, r => new ParentAccount {
            ParentId  = G.G<int>(r, "ParentId"),
            Phone     = G.G<string>(r, "Phone")     ?? "",
            Password  = G.G<string>(r, "Password")  ?? "",
            FullName  = G.G<string>(r, "FullName"),
            IsActive  = G.G<bool>(r, "IsActive"),
            CreatedAt = G.G<DateTime>(r, "CreatedAt"),
            LastLogin = G.G<DateTime?>(r, "LastLogin"),
        });
        return l.FirstOrDefault();
    }

    public int Save(ParentAccount p)
        => db.ExecOut("sp_ParentSave", new() {
            { "@ParentId", p.ParentId },
            { "@Phone",    p.Phone    },
            { "@Password", p.Password },
            { "@FullName", p.FullName },
        }, "@NewParentId");

    public void UpdateLastLogin(int parentId)
        => db.Exec("sp_ParentUpdateLastLogin", new() { { "@ParentId", parentId } });

    public List<ParentAccount> GetAllParents()
        => db.Sql("SELECT ParentId, Phone, Password, FullName, IsActive, CreatedAt, LastLogin FROM ParentAccounts ORDER BY CreatedAt DESC",
            r => new ParentAccount {
                ParentId  = G.G<int>(r, "ParentId"),
                Phone     = G.G<string>(r, "Phone")     ?? "",
                Password  = G.G<string>(r, "Password")  ?? "",
                FullName  = G.G<string>(r, "FullName"),
                IsActive  = G.G<bool>(r, "IsActive"),
                CreatedAt = G.G<DateTime>(r, "CreatedAt"),
                LastLogin = G.G<DateTime?>(r, "LastLogin"),
            });

    // sp_GetChildrenByParentPhone only excludes Status = 'Deleted', so Inactive students came through.
    // A parent must only ever see current (Active) students.
    // The portal is for the parents AND for the student: the phone may be the father's, the mother's or the student's own
    // number (the stored procedure only knew the first two, so a student could never register).
    public List<Student> GetChildren(string phone)
        => db.SqlP(@"
            SELECT s.StudentId, s.AdmissionNo, s.FullName, s.DateOfBirth, s.Gender,
                   s.FatherName, s.MotherName, s.Phone, s.FatherPhone, s.MotherPhone,
                   s.ProfilePicPath, s.RollNo, s.BloodGroup, s.Status, s.AdmissionDate,
                   s.AcademicYearId, ay.YearName,
                   s.ClassId,   c.ClassName,
                   s.SectionId, sec.SectionName,
                   s.BatchId,   b.BatchName
            FROM   Students s
            LEFT JOIN AcademicYears ay  ON ay.YearId     = s.AcademicYearId
            LEFT JOIN Classes       c   ON c.ClassId     = s.ClassId
            LEFT JOIN Sections      sec ON sec.SectionId = s.SectionId
            LEFT JOIN Batches       b   ON b.BatchId     = s.BatchId
            WHERE  (s.FatherPhone = @Phone OR s.MotherPhone = @Phone OR s.Phone = @Phone)
              AND  ISNULL(s.Status, 'Active') <> 'Deleted'
            ORDER BY s.FullName", new() { { "@Phone", phone } }, MapStudent)
             .Where(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase)).ToList();

    static Student MapStudent(SqlDataReader r) => new() {
        StudentId      = G.G<int>(r, "StudentId"),
        AdmissionNo    = G.G<string>(r, "AdmissionNo")    ?? "",
        FullName       = G.G<string>(r, "FullName")       ?? "",
        DateOfBirth    = G.G<DateTime?>(r, "DateOfBirth"),
        Gender         = G.G<string>(r, "Gender"),
        FatherName     = G.G<string>(r, "FatherName"),
        MotherName     = G.G<string>(r, "MotherName"),
        Phone          = G.G<string>(r, "Phone"),
        FatherPhone    = G.G<string>(r, "FatherPhone"),
        MotherPhone    = G.G<string>(r, "MotherPhone"),
        ProfilePicPath = G.G<string>(r, "ProfilePicPath"),
        RollNo         = G.G<string>(r, "RollNo"),
        BloodGroup     = G.G<string>(r, "BloodGroup"),
        Status         = G.G<string>(r, "Status")         ?? "Active",
        AdmissionDate  = G.G<DateTime?>(r, "AdmissionDate"),
        AcademicYearId = G.G<int?>(r, "AcademicYearId"),
        YearName       = G.G<string>(r, "YearName"),
        ClassId        = G.G<int?>(r, "ClassId"),
        ClassName      = G.G<string>(r, "ClassName"),
        SectionId      = G.G<int?>(r, "SectionId"),
        SectionName    = G.G<string>(r, "SectionName"),
        BatchId        = G.G<int?>(r, "BatchId"),
        BatchName      = G.G<string>(r, "BatchName"),
    };
}
