namespace SchoolMS.Domain;

public class Designation
{
    public int    DesignationId{get;set;}
    public string DesignationName{get;set;}="";
    public bool   IsActive{get;set;}=true;
}

public class Faculty
{
    public int       FacultyId{get;set;}
    public string    EmployeeCode{get;set;}="";
    public string    FullName{get;set;}="";
    public int?      DesignationId{get;set;}
    public string?   DesignationName{get;set;}
    public string?   Qualification{get;set;}
    public string?   Specialization{get;set;}
    public string?   Gender{get;set;}
    public DateTime? DateOfBirth{get;set;}
    public DateTime? DateOfJoining{get;set;}
    public string?   Phone{get;set;}
    public string?   AlternatePhone{get;set;}
    public string?   Email{get;set;}
    public string?   Address{get;set;}
    public string?   ProfilePicPath{get;set;}
    public decimal?  Salary{get;set;}
    public string?   BloodGroup{get;set;}
    public string?   AadharNo{get;set;}
    public string    Status{get;set;}="Active";
    public string?   Remarks{get;set;}
    public DateTime  CreatedAt{get;set;}
}

public class FacultySubject
{
    public int     FacultySubjectId{get;set;}
    public int     FacultyId{get;set;}
    public int?    ClassId{get;set;}
    public string? ClassName{get;set;}
    public int?    SubjectId{get;set;}
    public string? SubjectName{get;set;}
    public int?    SectionId{get;set;}
    public string? SectionName{get;set;}
}
