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
    public List<Class>   Classes{get;set;}=new();
    public List<Section> Sections{get;set;}=new();
    public List<Batch>   Batches{get;set;}=new();
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

public class ExpenseListVM
{
    public List<Expense> Expenses{get;set;}=new();
    public int Total{get;set;} public int Page{get;set;}=1; public int PageSize{get;set;}=15;
    public string? Search{get;set;} public int? CategoryFilter{get;set;} public int? MonthFilter{get;set;} public int? YearFilter{get;set;}
    public List<ExpenseCat> Categories{get;set;}=new();
    public int TotalPages => Math.Max(1,(int)Math.Ceiling((double)Total/PageSize));
}

public class ExpenseFormVM
{
    public Expense Expense{get;set;}=new();
    public List<ExpenseCat> Categories{get;set;}=new();
}
