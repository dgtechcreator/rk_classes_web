namespace SchoolMS.Domain;

public class DashboardStats
{
    public int     TotalStudents{get;set;}
    public int     NewToday{get;set;}
    public int     PresentToday{get;set;}
    public int     AbsentToday{get;set;}
    public decimal FeesThisMonth{get;set;}
    public decimal ExpensesThisMonth{get;set;}
    public int     Class1Count{get;set;}
    public int     Class2Count{get;set;}
    public int     Class3Count{get;set;}
    public int     TotalStaff{get;set;}
    public decimal TotalFeesOverall{get;set;}
    public decimal TotalCollectedOverall{get;set;}
    public decimal TotalDiscountOverall{get;set;}
    public decimal BalanceOverall{get;set;}
    public List<ClassStrengthStat> ClassStrengths{get;set;} = new();
    public List<ClassAllStat> AllClasses{get;set;} = new();
}

public class ClassStrengthStat
{
    public string ClassName{get;set;} = "";
    public int StudentCount{get;set;}
    public string Color{get;set;} = "#6d28d9";
}

public class ClassAllStat
{
    public int ClassId{get;set;}
    public string ClassName{get;set;} = "";
    public int OrderNo{get;set;}
    public int StudentCount{get;set;}
    public bool IsActive{get;set;} = true;
}

public class FinanceAcademicBreakdown
{
    public string ClassName{get;set;} = "";
    public string BatchName{get;set;} = "";
    public int StudentCount{get;set;}
    public decimal TotalFees{get;set;}
    public decimal EstimatedCollected{get;set;}
    public decimal EstimatedDiscount{get;set;}
}
