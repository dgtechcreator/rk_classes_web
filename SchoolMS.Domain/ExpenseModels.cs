namespace SchoolMS.Domain;

public class Expense
{
    public int      ExpenseId{get;set;}
    public string   ExpenseNo{get;set;}="";
    public int?     CategoryId{get;set;}
    public string?  CategoryName{get;set;}
    public string   Title{get;set;}="";
    public string?  Description{get;set;}
    public decimal  Amount{get;set;}
    public DateTime ExpenseDate{get;set;}=DateTime.Today;
    public string   PaymentMode{get;set;}="Cash";
    public string?  BillNo{get;set;}
    public string?  VendorName{get;set;}
    public string?  EnteredByName{get;set;}
    public DateTime CreatedAt{get;set;}
    /// <summary>"Expense" = a normal expense; "Salary" = a PAID teacher payment shown as an expense (read-only here, managed in Teacher Payment).</summary>
    public string   Source{get;set;}="Expense";
    public bool     IsSalary => Source == "Salary";
}

/// <summary>Filters of the Expenses list. [CategoryId] -1 = Teacher Salary. The date range is inclusive.</summary>
public class ExpenseQuery
{
    public string?   Search{get;set;}
    public int?      CategoryId{get;set;}
    public DateTime? From{get;set;}
    public DateTime? To{get;set;}
    public int       Page{get;set;}=1;
    public int       PageSize{get;set;}=15;
}

public class ExpenseCategoryTotal
{
    public string  Category{get;set;}="";
    public int     Count{get;set;}
    public decimal Amount{get;set;}
}

/// <summary>One page of expenses (normal expenses + paid teacher salaries) with the totals of EVERYTHING that matches the filter.</summary>
public class ExpenseListResult
{
    public List<Expense> Items{get;set;}=new();
    public int           Total{get;set;}
    public decimal       TotalAmount{get;set;}
    public List<ExpenseCategoryTotal> ByCategory{get;set;}=new();
}

public static class ExpenseRange
{
    public const int SalaryCategoryId = -1;

    /// <summary>
    /// Picks the date range of the list: "yyyy-MM" = that month, [all] = no limit, an explicit from / to, the old month / year numbers
    /// (older app builds), otherwise the current month. Returns the range plus a label for the screen.
    /// </summary>
    public static (DateTime? from, DateTime? to, string mode) Resolve(string? ym, DateTime? from, DateTime? to, bool all, int? month = null, int? year = null)
    {
        if (all) return (null, null, "all");
        if (from != null || to != null) return (from?.Date, to?.Date, "range");
        if (!string.IsNullOrWhiteSpace(ym) && DateTime.TryParseExact(ym + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var m))
            return (m, m.AddMonths(1).AddDays(-1), "month");
        if (month.HasValue || year.HasValue)
        {
            var y = year ?? DateTime.Today.Year;
            if (month.HasValue) { var f = new DateTime(y, Math.Clamp(month.Value, 1, 12), 1); return (f, f.AddMonths(1).AddDays(-1), "month"); }
            return (new DateTime(y, 1, 1), new DateTime(y, 12, 31), "range");
        }
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return (first, first.AddMonths(1).AddDays(-1), "month");
    }
}
