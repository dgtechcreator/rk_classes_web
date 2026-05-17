namespace SchoolMS.Domain;

public class Student
{
    public int       StudentId{get;set;}
    public string    AdmissionNo{get;set;}="";
    public string    FullName{get;set;}="";
    public DateTime? DateOfBirth{get;set;}
    public string?   Gender{get;set;}
    public string?   FatherName{get;set;}
    public string?   MotherName{get;set;}
    public string?   Phone{get;set;}
    public string?   FatherPhone{get;set;}
    public string?   MotherPhone{get;set;}
    public string?   Email{get;set;}
    public string?   Address{get;set;}
    public string?   ProfilePicPath{get;set;}
    public int?      AcademicYearId{get;set;}
    public string?   YearName{get;set;}
    public int?      ClassId{get;set;}
    public string?   ClassName{get;set;}
    public int?      SectionId{get;set;}
    public string?   SectionName{get;set;}
    public int?      BatchId{get;set;}
    public string?   BatchName{get;set;}
    public string?   RollNo{get;set;}
    public string?   BloodGroup{get;set;}
    public string    Status{get;set;}="Active";
    public DateTime  CreatedAt{get;set;}
}
