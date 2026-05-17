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
}
