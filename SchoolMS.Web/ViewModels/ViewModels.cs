using SchoolMS.Domain;

namespace SchoolMS.Web.ViewModels;

public class LoginVM { public string Username{get;set;}=""; public string Password{get;set;}=""; public string? Error{get;set;} }

public class StudentListVM
{
    public List<Student>  Students{get;set;}=new();
    public int Total{get;set;} public int Page{get;set;}=1; public int PageSize{get;set;}=15;
    public string? Search{get;set;} public int? ClassFilter{get;set;} public int? SectionFilter{get;set;} public int? BatchFilter{get;set;} public string? StatusFilter{get;set;}
    public List<Class>   Classes{get;set;}=new();
    public List<Section> Sections{get;set;}=new();
    public List<Batch>   Batches{get;set;}=new();
    public int TotalPages => Math.Max(1,(int)Math.Ceiling((double)Total/PageSize));
}

public class StudentFormVM
{
    public Student Student{get;set;}=new();
    public List<AcademicYear> Years{get;set;}=new();
    public List<Class>    Classes{get;set;}=new();
    public List<Section>  Sections{get;set;}=new();
    public List<Batch>    Batches{get;set;}=new();
    public IFormFile?     ProfilePic{get;set;}
}

public class AttendanceVM
{
    public List<AttendanceRecord> Records{get;set;}=new();
    public DateTime Date{get;set;}=DateTime.Today;
    public int? ClassId{get;set;} public int? SectionId{get;set;} public int? BatchId{get;set;}
    public string? Subject{get;set;}
    public string? SirName{get;set;}
    public string? StartTime{get;set;}
    public string? EndTime{get;set;}
    public List<Class>   Classes{get;set;}=new();
    public List<Section> Sections{get;set;}=new();
    public List<Batch>   Batches{get;set;}=new();
    public List<Subject> Subjects{get;set;}=new();
    public List<Faculty> Teachers{get;set;}=new();
    public int TotalPresent{get;set;}
    public int TotalAbsent{get;set;}
    public int TotalMarked{get;set;}
    public List<ClassAttendanceSummary> ClassSummaries{get;set;}=new();
    public List<dynamic> AllSessions{get;set;}=new();
}

public class ClassAttendanceSummary
{
    public int ClassId{get;set;}
    public string ClassName{get;set;}="";
    public string? SectionName{get;set;}
    public string? BatchName{get;set;}
    public int Present{get;set;}
    public int Absent{get;set;}
    public int Total{get;set;}
}

public class MarksVM
{
    public List<TestMark> Marks{get;set;}=new();
    public int? ExamId{get;set;} public int? ClassId{get;set;}
    public List<Exam>    Exams{get;set;}=new();
    public List<Class>   Classes{get;set;}=new();
    public List<Subject> Subjects{get;set;}=new();
}

public class FeesListVM
{
    public List<FeePayment> Payments{get;set;}=new();
    public int Total{get;set;} public int Page{get;set;}=1; public int PageSize{get;set;}=15;
    public string? Search{get;set;} public string? MonthFilter{get;set;} public int? FeeTypeFilter{get;set;}
    public List<FeeType> FeeTypes{get;set;}=new();
    public decimal GrandTotalAmount{get;set;} = 0;
    public int TotalPages => Math.Max(1,(int)Math.Ceiling((double)Total/PageSize));
}

public class FeeCollectVM
{
    public int StudentId{get;set;} public int FeeTypeId{get;set;}
    public int? BatchId{get;set;} public int? ClassId{get;set;} public int? SectionId{get;set;}
    public decimal Amount{get;set;} public decimal Discount{get;set;} public decimal LateFine{get;set;}
    public string PaymentMode{get;set;}="Cash"; public string? TransactionRef{get;set;}
    public string? Month{get;set;} public string? Remarks{get;set;} public int? AcademicYearId{get;set;}
    public List<Student>      Students{get;set;}=new();
    public List<FeeType>      FeeTypes{get;set;}=new();
    public List<AcademicYear> Years{get;set;}=new();
    public List<Class>        Classes{get;set;}=new();
    public List<Batch>        Batches{get;set;}=new();
    public List<Section>      Sections{get;set;}=new();
}

// New ViewModel for Pay page (auto-loaded student + fee data)
public class FeePayVM
{
    public SchoolMS.Domain.Student Student{get;set;}=new();
    public decimal ActualFee{get;set;}
    public decimal TotalPaid{get;set;}
    public decimal Balance{get;set;}
    public decimal ExistingDiscount{get;set;}
    public DateTime? DueDate{get;set;}
    public decimal AdditionalDiscount{get;set;}
    public decimal PayingNow{get;set;}
    public DateTime PaymentDate{get;set;}=DateTime.Today;
    public string PaymentMode{get;set;}="Cash";
    public string? TransactionRef{get;set;}
    public string? Remarks{get;set;}
    public int? EditPaymentId{get;set;}
    // Fee heads breakdown
    public List<SchoolMS.Domain.FeeStructure> FeeStructures{get;set;}=new();
    // Previous payments for this student
    public List<SchoolMS.Domain.FeePayment> PaymentHistory{get;set;}=new();

    public bool DiscountAlreadyGiven => ExistingDiscount > 0;
}

// New ViewModel for Fees Index (student search)
public class FeesStudentListVM
{
    public List<SchoolMS.Domain.Student>      Students{get;set;}=new();
    public List<SchoolMS.Domain.AcademicYear> Years{get;set;}=new();
    public List<SchoolMS.Domain.Class>        Classes{get;set;}=new();
    public List<SchoolMS.Domain.Section>      Sections{get;set;}=new();
    public int? YearFilter{get;set;}
    public int? ClassFilter{get;set;}
    public int? SectionFilter{get;set;}
    public bool Searched{get;set;}
    // Fee structures for the selected class – used to show fee amounts in list
    public List<SchoolMS.Domain.FeeStructure> FeeStructures{get;set;}=new();
}

public class ExpenseListVM
{
    public List<Expense> Expenses{get;set;}=new();
    public int Total{get;set;} public int Page{get;set;}=1; public int PageSize{get;set;}=15;
    public string? Search{get;set;} public int? CategoryFilter{get;set;} public int? MonthFilter{get;set;} public int? YearFilter{get;set;}
    public List<ExpenseCat>    Categories{get;set;}=new();
    public List<AcademicYear>  Years{get;set;}=new();
    public int TotalPages => Math.Max(1,(int)Math.Ceiling((double)Total/PageSize));
}

public class ExpenseFormVM
{
    public Expense Expense{get;set;}=new();
    public List<ExpenseCat>   Categories{get;set;}=new();
    public List<AcademicYear> Years{get;set;}=new();
    public int? AcademicYearId{get;set;}
}

// ── Parent Portal ViewModels ──────────────────────────────────────────────────
public class ParentLoginVM
{
    public string  Phone    { get; set; } = "";
    public string  Password { get; set; } = "";
    public string? Error    { get; set; }
    public bool    ShowRegister { get; set; } = false;
    // Register fields
    public string? RegPhone     { get; set; }
    public string? RegPassword  { get; set; }
    public string? RegPassword2 { get; set; }
    public string? RegFullName  { get; set; }
    public string? RegError     { get; set; }
}

public class ParentDashboardVM
{
    public List<Student>       Children       { get; set; } = new();
    public Student?            SelectedChild  { get; set; }
    public AttendanceReport?   Attendance     { get; set; }
    public List<StudentAttendanceDetail> AttDetail { get; set; } = new();
    public List<TestMark>      Marks          { get; set; } = new();
    public List<FeePayment>    FeeHistory     { get; set; } = new();
    public decimal             ActualFee      { get; set; }
    public decimal             TotalPaid      { get; set; }
    public decimal             Balance        { get; set; }
    public string              ActiveTab      { get; set; } = "attendance";
    public string?             ParentName     { get; set; }
    public string?             ParentPhone    { get; set; }
}
