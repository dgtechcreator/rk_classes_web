namespace SchoolMS.Domain;

public class FeePayment
{
    public int      PaymentId{get;set;}
    public string   ReceiptNo{get;set;}="";
    public int      StudentId{get;set;}
    public string?  StudentName{get;set;}
    public string?  AdmissionNo{get;set;}
    public string?  ClassName{get;set;}
    public string?  SectionName{get;set;}
    public string?  BatchName{get;set;}
    public int?     FeeTypeId{get;set;}
    public string?  FeeTypeName{get;set;}
    public decimal  Amount{get;set;}
    public decimal  Discount{get;set;}
    public decimal  LateFine{get;set;}
    public decimal  NetAmount{get;set;}
    public DateTime PaymentDate{get;set;}
    public DateTime? DueDate{get;set;}
    public string   PaymentMode{get;set;}="Cash";
    public string?  TransactionRef{get;set;}
    public string?  Month{get;set;}
    public string?  Remarks{get;set;}
    public string?  CollectorName{get;set;}
    public string?  StudentPhone{get;set;}
    public string?  FatherPhone{get;set;}
    public string?  StudentEmail{get;set;}
    public string?  StudentAddress{get;set;}
    public DateTime CreatedAt{get;set;}
    public bool     IsDeleted{get;set;}
    public DateTime? DeletedAt{get;set;}
    public string?  DeletedByName{get;set;}
}

public class FeeStructure
{
    public int      StructureId{get;set;}
    public int      AcademicYearId{get;set;}
    public string?  YearName{get;set;}
    public int      ClassId{get;set;}
    public string?  ClassName{get;set;}
    public int      SectionId{get;set;}
    public string?  SectionName{get;set;}
    public int      FeeTypeId{get;set;}
    public string?  FeeTypeName{get;set;}
    public decimal  Amount{get;set;}
    public int      DueDay{get;set;}=10;
    public bool     IsMonthly{get;set;}=true;
    public string?  Remarks{get;set;}
    public bool     IsActive{get;set;}=true;
    public string?  CreatedByName{get;set;}
    public DateTime CreatedAt{get;set;}
}

public class StudentFee
{
    public int      StudentFeeId{get;set;}
    public int      StudentId{get;set;}
    public string?  StudentName{get;set;}
    public string?  AdmissionNo{get;set;}
    public string?  RollNo{get;set;}
    public string?  ClassName{get;set;}
    public string?  SectionName{get;set;}
    public int      StructureId{get;set;}
    public int      AcademicYearId{get;set;}
    public string?  YearName{get;set;}
    public int      FeeTypeId{get;set;}
    public string?  FeeTypeName{get;set;}
    public string?  Month{get;set;}
    public DateTime? DueDate{get;set;}
    public decimal  Amount{get;set;}
    public decimal  PaidAmount{get;set;}
    public decimal  Discount{get;set;}
    public decimal  LateFine{get;set;}
    public decimal  BalanceAmount{get;set;}
    public string   Status{get;set;}="Pending";
    public DateTime CreatedAt{get;set;}
}

public class FeeStructureSummary
{
    public string? ClassName{get;set;}
    public string? SectionName{get;set;}
    public string? YearName{get;set;}
    public int     StudentCount{get;set;}
    public decimal TotalMonthlyFee{get;set;}
    public int     FeeHeads{get;set;}
    public decimal CollectedAmt{get;set;}
    public decimal PendingAmt{get;set;}
}

public class ClassFeeSetup
{
    public int     SetupId{get;set;}
    public int     BatchId{get;set;}
    public string? BatchName{get;set;}
    public int     ClassId{get;set;}
    public string? ClassName{get;set;}
    public int     SectionId{get;set;}
    public string? SectionName{get;set;}
    public decimal Amount{get;set;}
    public bool    IsActive{get;set;}=true;
}

public class OverallFeesSummary
{
    public decimal TotalFeesOwed{get;set;}
    public decimal TotalCollected{get;set;}
    public decimal TotalDiscount{get;set;}
    public decimal Balance{get;set;}
}
