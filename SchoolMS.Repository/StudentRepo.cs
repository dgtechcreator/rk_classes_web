using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class StudentRepo(CommonConnectivity db)
{
    public (List<Student> data, int total) GetAll(int pg, int ps, string? search, int? cls, int? sec, int? bat, int? yr, string? status)
    {
        var op = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var data = db.ReadPaged("sp_GetStudents", new() {
            { "@PageNo",pg }, { "@PageSize",ps }, { "@Search",search }, { "@ClassId",cls },
            { "@SectionId",sec }, { "@BatchId",bat }, { "@AcademicYearId",yr }, { "@Status",status }
        }, MapStudent, op);
        return (data, op.Value is DBNull ? 0 : Convert.ToInt32(op.Value));
    }
    public Student? GetById(int id)
    {
        var l = db.Read("sp_GetStudentById", new() { { "@StudentId", id } }, MapStudent);
        return l.FirstOrDefault();
    }
    public void Delete(int id)  => db.Exec("sp_DeleteStudent",  new() { { "@StudentId", id } });
    public void Restore(int id) => db.Exec("sp_RestoreStudent", new() { { "@StudentId", id } });

    public int Save(Student s, string? pic, int by) => db.ExecOut("sp_SaveStudent", new() {
        { "@StudentId",s.StudentId }, { "@AdmissionNo",s.AdmissionNo==""?null:s.AdmissionNo },
        { "@FullName",s.FullName }, { "@DateOfBirth",s.DateOfBirth }, { "@Gender",s.Gender },
        { "@FatherName",s.FatherName }, { "@MotherName",s.MotherName }, { "@Phone",s.Phone },
        { "@FatherPhone",s.FatherPhone }, { "@MotherPhone",s.MotherPhone },
        { "@Email",s.Email }, { "@Address",s.Address }, { "@ProfilePicPath",pic },
        { "@AcademicYearId",s.AcademicYearId }, { "@ClassId",s.ClassId }, { "@SectionId",s.SectionId },
        { "@BatchId",s.BatchId }, { "@RollNo",s.RollNo }, { "@BloodGroup",s.BloodGroup },
        { "@Status",s.Status }, { "@AdmissionDate",s.AdmissionDate },
        { "@CreatedBy",by }
    }, "@NewStudentId");

    static Student MapStudent(SqlDataReader r) => new() {
        StudentId=G.G<int>(r,"StudentId"), AdmissionNo=G.G<string>(r,"AdmissionNo")??"",
        FullName=G.G<string>(r,"FullName")??"", DateOfBirth=G.G<DateTime?>(r,"DateOfBirth"),
        Gender=G.G<string>(r,"Gender"), FatherName=G.G<string>(r,"FatherName"),
        MotherName=G.G<string>(r,"MotherName"), Phone=G.G<string>(r,"Phone"),
        FatherPhone=G.G<string>(r,"FatherPhone"), MotherPhone=G.G<string>(r,"MotherPhone"),
        Email=G.G<string>(r,"Email"), Address=G.G<string>(r,"Address"),
        ProfilePicPath=G.G<string>(r,"ProfilePicPath"),
        AcademicYearId=G.G<int?>(r,"AcademicYearId"), YearName=G.G<string>(r,"YearName"),
        ClassId=G.G<int?>(r,"ClassId"), ClassName=G.G<string>(r,"ClassName"),
        SectionId=G.G<int?>(r,"SectionId"), SectionName=G.G<string>(r,"SectionName"),
        BatchId=G.G<int?>(r,"BatchId"), BatchName=G.G<string>(r,"BatchName"),
        RollNo=G.G<string>(r,"RollNo"), BloodGroup=G.G<string>(r,"BloodGroup"),
        Status=G.G<string>(r,"Status")??"Active", CreatedAt=G.G<DateTime>(r,"CreatedAt"),
        AdmissionDate=G.G<DateTime?>(r,"AdmissionDate"),
        Nationality=G.G<string>(r,"Nationality"), MotherTongue=G.G<string>(r,"MotherTongue"),
        Religion=G.G<string>(r,"Religion"), PlaceOfBirth=G.G<string>(r,"PlaceOfBirth"),
        AadhaarNo=G.G<string>(r,"AadhaarNo"), AlternatePhone=G.G<string>(r,"AlternatePhone"),
        PreviousPercentage=G.G<string>(r,"PreviousPercentage"), PreviousSchool=G.G<string>(r,"PreviousSchool"),
        Board=G.G<string>(r,"Board"), FatherOccupation=G.G<string>(r,"FatherOccupation"),
        MotherOccupation=G.G<string>(r,"MotherOccupation"), GuardianName=G.G<string>(r,"GuardianName"),
        GuardianOccupation=G.G<string>(r,"GuardianOccupation"), GuardianPhone=G.G<string>(r,"GuardianPhone"),
        City=G.G<string>(r,"City"), State=G.G<string>(r,"State"), District=G.G<string>(r,"District"),
        Pincode=G.G<string>(r,"Pincode"), PermanentAddress=G.G<string>(r,"PermanentAddress"),
        CreatedBy=G.G<int?>(r,"CreatedBy"), CreatedByName=G.G<string>(r,"CreatedByName"),
        DeletedBy=G.G<int?>(r,"DeletedBy"), DeletedByName=G.G<string>(r,"DeletedByName")
    };
}
