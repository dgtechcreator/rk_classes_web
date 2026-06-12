using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class FacultyRepo(CommonConnectivity db)
{
    public (List<Faculty> data, int total) GetAll(string? search, string? status, int? desigId, int pg, int ps)
    {
        var op = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var data = db.ReadPaged("sp_GetFaculty", new() {
            {"@Search",search}, {"@Status",status}, {"@DesignationId",desigId},
            {"@PageNo",pg}, {"@PageSize",ps}
        }, MapFaculty, op);
        return (data, op.Value is DBNull ? 0 : Convert.ToInt32(op.Value));
    }

    public Faculty? GetById(int id)
    {
        var l = db.Read("sp_GetFacultyById", new() { {"@FacultyId",id} }, MapFaculty);
        return l.FirstOrDefault();
    }

    public List<FacultySubject> GetSubjects(int facultyId)
        => db.Sql($@"SELECT fs.*,c.ClassName,sub.SubjectName,sec.SectionName
            FROM FacultySubjects fs
            LEFT JOIN Classes c ON c.ClassId=fs.ClassId
            LEFT JOIN Subjects sub ON sub.SubjectId=fs.SubjectId
            LEFT JOIN Sections sec ON sec.SectionId=fs.SectionId
            WHERE fs.FacultyId={facultyId} AND fs.IsActive=1",
            r => new FacultySubject {
                FacultySubjectId = G.G<int>(r,"FacultySubjectId"),
                FacultyId        = G.G<int>(r,"FacultyId"),
                ClassId          = G.G<int?>(r,"ClassId"),
                ClassName        = G.G<string>(r,"ClassName"),
                SubjectId        = G.G<int?>(r,"SubjectId"),
                SubjectName      = G.G<string>(r,"SubjectName"),
                SectionId        = G.G<int?>(r,"SectionId"),
                SectionName      = G.G<string>(r,"SectionName")
            });

    public void Delete(int id)  => db.Exec("sp_DeleteFaculty",  new() { { "@FacultyId", id } });
    public void Restore(int id) => db.Exec("sp_RestoreFaculty", new() { { "@FacultyId", id } });

    public int Save(Faculty f, string? pic, int by)
        => db.ExecOut("sp_SaveFaculty", new() {
            {"@FacultyId",f.FacultyId}, {"@EmployeeCode",f.EmployeeCode==""?null:f.EmployeeCode},
            {"@FullName",f.FullName}, {"@DesignationId",f.DesignationId},
            {"@Qualification",f.Qualification}, {"@Specialization",f.Specialization},
            {"@Gender",f.Gender}, {"@DateOfBirth",f.DateOfBirth},
            {"@DateOfJoining",f.DateOfJoining}, {"@Phone",f.Phone},
            {"@AlternatePhone",f.AlternatePhone}, {"@Email",f.Email},
            {"@Address",f.Address}, {"@ProfilePicPath",pic},
            {"@Salary",f.Salary}, {"@BloodGroup",f.BloodGroup},
            {"@AadharNo",f.AadharNo}, {"@Status",f.Status},
            {"@Remarks",f.Remarks}, {"@CreatedBy",by}
        }, "@NewFacultyId");

    public List<Designation> GetDesignations()
        => db.Read("sp_GetDesignations", new(), r => new Designation {
            DesignationId   = G.G<int>(r,"DesignationId"),
            DesignationName = G.G<string>(r,"DesignationName")??"",
            IsActive        = G.G<bool>(r,"IsActive")
        });

    public int SaveDesignation(Designation d)
        => db.ExecOut("sp_SaveDesignation", new() {
            {"@DesignationId",d.DesignationId},
            {"@DesignationName",d.DesignationName},
            {"@IsActive",d.IsActive}
        }, "@NewId");

    static Faculty MapFaculty(SqlDataReader r) => new() {
        FacultyId       = G.G<int>(r,"FacultyId"),
        EmployeeCode    = G.G<string>(r,"EmployeeCode")??"",
        FullName        = G.G<string>(r,"FullName")??"",
        DesignationId   = G.G<int?>(r,"DesignationId"),
        DesignationName = G.G<string>(r,"DesignationName"),
        Qualification   = G.G<string>(r,"Qualification"),
        Specialization  = G.G<string>(r,"Specialization"),
        Gender          = G.G<string>(r,"Gender"),
        DateOfBirth     = G.G<DateTime?>(r,"DateOfBirth"),
        DateOfJoining   = G.G<DateTime?>(r,"DateOfJoining"),
        Phone           = G.G<string>(r,"Phone"),
        AlternatePhone  = G.G<string>(r,"AlternatePhone"),
        Email           = G.G<string>(r,"Email"),
        Address         = G.G<string>(r,"Address"),
        ProfilePicPath  = G.G<string>(r,"ProfilePicPath"),
        Salary          = G.G<decimal?>(r,"Salary"),
        BloodGroup      = G.G<string>(r,"BloodGroup"),
        AadharNo        = G.G<string>(r,"AadharNo"),
        Status          = G.G<string>(r,"Status")??"Active",
        Remarks         = G.G<string>(r,"Remarks"),
        CreatedAt       = G.G<DateTime>(r,"CreatedAt"),
        CreatedBy       = G.G<int?>(r,"CreatedBy"),
        CreatedByName   = G.G<string>(r,"CreatedByName")
    };
}
