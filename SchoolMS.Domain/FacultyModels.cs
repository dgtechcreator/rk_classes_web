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
    public int?      CreatedBy{get;set;}
    public string?   CreatedByName{get;set;}
    public int?      DeletedBy{get;set;}
    public string?   DeletedByName{get;set;}
    public DateTime? DeletedAt{get;set;}

    /// <summary>Returns "Firstname ma'am" for Female, "Firstname sir" for Male/other.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(FullName) ? FullName
        : FullName.Trim() + (Gender == "Female" ? " MA'AM" : " SIR");
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

public class TeacherPayment
{
    public int TeacherPaymentId{get;set;}
    public int FacultyId{get;set;}
    public string? FacultyName{get;set;}
    public string PaymentType{get;set;} = ""; // Hourly, Topic, Fixed
    public decimal Rate{get;set;}
    public decimal? Quantity{get;set;} // Hours or Topics
    public decimal TotalAmount{get;set;}
    public int PaymentMonth{get;set;} // 1-12
    public int PaymentYear{get;set;}
    public bool IsPaid{get;set;}
    public DateTime? PaymentDate{get;set;}
    public string? PaymentMode{get;set;}
    public string? TransactionRef{get;set;}
    public string? ReceiptNo{get;set;}
    public string? Remarks{get;set;}
    public DateTime CreatedAt{get;set;}
    public int? CreatedBy{get;set;}
    public DateTime? UpdatedAt{get;set;}
    public int? UpdatedBy{get;set;}
    public bool IsDeleted{get;set;}
}

public class TeacherPaymentTeacherRow
{
    public int FacultyId{get;set;}
    public string FacultyName{get;set;} = "";
    public int Count{get;set;}
    public decimal PaidAmount{get;set;}
    public decimal PendingAmount{get;set;}
    public DateTime? LastPaidOn{get;set;}
}

public class TeacherPaymentSummary
{
    public int Count{get;set;}
    public int PaidCount{get;set;}
    public int PendingCount{get;set;}
    public decimal TotalAmount{get;set;}
    public decimal PaidAmount{get;set;}
    public decimal PendingAmount{get;set;}
    public List<TeacherPaymentTeacherRow> Teachers{get;set;} = new();
}
