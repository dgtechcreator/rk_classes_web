namespace SchoolMS.Domain;

public class Student
{
    public int       StudentId{get;set;}
    public string    AdmissionNo{get;set;}="";
    public string    FullName{get;set;}="";
    /// <summary>"Surname StudentName FatherName MotherName" — what every list/card shows. FullName stays as typed for the edit form.</summary>
    public string    DisplayName => StudentNameFormatter.Format(FullName, FatherName, MotherName);
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
    public DateTime? AdmissionDate{get;set;}
    public string?   Nationality{get;set;}
    public string?   MotherTongue{get;set;}
    public string?   Religion{get;set;}
    public string?   PlaceOfBirth{get;set;}
    public string?   AadhaarNo{get;set;}
    public string?   AlternatePhone{get;set;}
    public string?   PreviousPercentage{get;set;}
    public string?   PreviousSchool{get;set;}
    public string?   Board{get;set;}
    public string?   FatherOccupation{get;set;}
    public string?   MotherOccupation{get;set;}
    public string?   GuardianName{get;set;}
    public string?   GuardianOccupation{get;set;}
    public string?   GuardianPhone{get;set;}
    public string?   City{get;set;}
    public string?   State{get;set;}
    public string?   District{get;set;}
    public string?   Pincode{get;set;}
    public string?   PermanentAddress{get;set;}
    public DateTime  CreatedAt{get;set;}
    public int?      CreatedBy{get;set;}
    public string?   CreatedByName{get;set;}
    public int?      DeletedBy{get;set;}
    public string?   DeletedByName{get;set;}
    public DateTime? DeletedAt{get;set;}
}
