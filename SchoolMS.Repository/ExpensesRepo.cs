using SchoolMS.DB;
using SchoolMS.Domain;
using Microsoft.Data.SqlClient;
using System.Data;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class ExpensesRepo(CommonConnectivity db)
{
    public (List<Expense> data, int total) GetAll(int pg, int ps, string? search, int? cat, int? month, int? year)
    {
        var op = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var data = db.ReadPaged("sp_GetExpenses", new() {
            { "@PageNo",pg }, { "@PageSize",ps }, { "@Search",search },
            { "@CategoryId",cat }, { "@Month",month }, { "@Year",year }
        }, MapExp, op);
        return (data, op.Value is DBNull ? 0 : Convert.ToInt32(op.Value));
    }
    /// <summary>
    /// The Expenses list: normal expenses plus PAID teacher payments (salary is money going out too), filtered, paged, with the totals of
    /// everything that matches. Falls back to normal expenses only when the TeacherPayments table does not exist.
    /// </summary>
    public ExpenseListResult Query(ExpenseQuery q)
    {
        try { return Run(q, true); }
        catch (SqlException) { return Run(q, false); }
    }

    ExpenseListResult Run(ExpenseQuery q, bool salary)
    {
        const string expenses = @"
            SELECT 'E' AS Src, e.ExpenseId AS Id, e.ExpenseNo AS No, e.CategoryId AS CategoryId, ISNULL(ec.CategoryName, N'Uncategorised') AS CategoryName,
                   e.Title AS Title, e.Description AS Description, e.Amount AS Amount, CAST(e.ExpenseDate AS DATE) AS D, e.PaymentMode AS PaymentMode,
                   e.BillNo AS BillNo, e.VendorName AS VendorName, u.FullName AS EnteredByName, e.CreatedAt AS CreatedAt
            FROM Expenses e
            LEFT JOIN ExpenseCategories ec ON ec.CategoryId = e.CategoryId
            LEFT JOIN Users u ON u.UserId = e.EnteredBy
            WHERE e.DeletedAt IS NULL";
        const string salaries = @"
            UNION ALL
            SELECT 'T', tp.TeacherPaymentId, N'SAL-' + CAST(tp.TeacherPaymentId AS NVARCHAR(12)), -1, N'Teacher Salary',
                   f.FullName + N' - salary ' + DATENAME(MONTH, DATEFROMPARTS(tp.PaymentYear, tp.PaymentMonth, 1)) + N' ' + CAST(tp.PaymentYear AS NVARCHAR(4)),
                   tp.Remarks, tp.TotalAmount, CAST(COALESCE(tp.PaymentDate, tp.CreatedAt) AS DATE), ISNULL(tp.PaymentMode, N'Cash'),
                   tp.ReceiptNo, f.FullName, cu.FullName, tp.CreatedAt
            FROM TeacherPayments tp
            JOIN Faculty f ON f.FacultyId = tp.FacultyId
            LEFT JOIN Users cu ON cu.UserId = tp.CreatedBy
            WHERE tp.IsPaid = 1 AND ISNULL(tp.IsDeleted, 0) = 0";
        string cte = "WITH u AS (" + expenses + (salary ? salaries : "") + ")";
        const string where = @"
            WHERE (@S IS NULL OR Title LIKE '%' + @S + '%' OR ISNULL(VendorName, '') LIKE '%' + @S + '%' OR No LIKE '%' + @S + '%')
              AND (@Cat IS NULL OR (@Cat = -1 AND Src = 'T') OR (@Cat <> -1 AND Src = 'E' AND CategoryId = @Cat))
              AND (@From IS NULL OR D >= @From) AND (@To IS NULL OR D <= @To)";
        object? S(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        Dictionary<string, object?> P() => new() { { "@S", S(q.Search) }, { "@Cat", q.CategoryId }, { "@From", q.From?.Date }, { "@To", q.To?.Date } };

        var res = new ExpenseListResult();
        var totals = db.SqlP(cte + " SELECT COUNT(*) AS C, ISNULL(SUM(Amount), 0) AS T FROM u" + where, P(),
            r => (C: G.G<int>(r, "C"), T: G.G<decimal>(r, "T"))).First();
        res.Total = totals.C; res.TotalAmount = totals.T;
        res.ByCategory = db.SqlP(cte + " SELECT CategoryName, COUNT(*) AS C, SUM(Amount) AS T FROM u" + where + " GROUP BY CategoryName ORDER BY SUM(Amount) DESC", P(),
            r => new ExpenseCategoryTotal { Category = G.G<string>(r, "CategoryName") ?? "", Count = G.G<int>(r, "C"), Amount = G.G<decimal>(r, "T") });
        var p = P();
        p["@Skip"] = Math.Max(0, (q.Page - 1) * q.PageSize); p["@Take"] = Math.Clamp(q.PageSize, 1, 500);
        res.Items = db.SqlP(cte + " SELECT * FROM u" + where + " ORDER BY D DESC, CreatedAt DESC, Id DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY", p,
            r => new Expense {
                Source = G.G<string>(r, "Src") == "T" ? "Salary" : "Expense",
                ExpenseId = G.G<int>(r, "Id"), ExpenseNo = G.G<string>(r, "No") ?? "",
                CategoryId = G.G<int?>(r, "CategoryId"), CategoryName = G.G<string>(r, "CategoryName"),
                Title = G.G<string>(r, "Title") ?? "", Description = G.G<string>(r, "Description"),
                Amount = G.G<decimal>(r, "Amount"), ExpenseDate = G.G<DateTime>(r, "D"),
                PaymentMode = G.G<string>(r, "PaymentMode") ?? "Cash", BillNo = G.G<string>(r, "BillNo"),
                VendorName = G.G<string>(r, "VendorName"), EnteredByName = G.G<string>(r, "EnteredByName"),
                CreatedAt = G.G<DateTime>(r, "CreatedAt"),
            });
        return res;
    }

    public Expense? GetById(int id)
        => db.SqlP(@"SELECT e.*, ec.CategoryName, u.FullName AS EnteredByName FROM Expenses e
                     LEFT JOIN ExpenseCategories ec ON ec.CategoryId = e.CategoryId
                     LEFT JOIN Users u ON u.UserId = e.EnteredBy
                     WHERE e.ExpenseId = @Id AND e.DeletedAt IS NULL", new() { { "@Id", id } }, MapExp).FirstOrDefault();

    /// <summary>Salary paid (teacher payments marked paid) between two dates - part of what goes out, like any expense.</summary>
    public decimal SalaryBetween(DateTime from, DateTime to)
    {
        try
        {
            return db.SqlP(@"SELECT ISNULL(SUM(TotalAmount), 0) AS T FROM TeacherPayments
                             WHERE IsPaid = 1 AND ISNULL(IsDeleted, 0) = 0 AND CAST(COALESCE(PaymentDate, CreatedAt) AS DATE) BETWEEN @F AND @T",
                new() { { "@F", from.Date }, { "@T", to.Date } }, r => G.G<decimal>(r, "T")).FirstOrDefault();
        }
        catch { return 0; }
    }

    public int Save(Expense e, int by) => db.ExecOut("sp_SaveExpense", new() {
        { "@ExpenseId",e.ExpenseId }, { "@CategoryId",e.CategoryId }, { "@Title",e.Title },
        { "@Description",e.Description }, { "@Amount",e.Amount }, { "@ExpenseDate",e.ExpenseDate },
        { "@PaymentMode",e.PaymentMode }, { "@BillNo",e.BillNo }, { "@VendorName",e.VendorName },
        { "@EnteredBy",by }
    }, "@NewExpenseId");
    public void Delete(int expenseId, int deletedBy) => db.Exec("sp_DeleteExpense", new() { { "@ExpenseId",expenseId }, { "@DeletedBy",deletedBy } });
    public List<DeletedItem> GetDeleted()
        => db.Sql(@"SELECT e.ExpenseId, e.ExpenseNo, e.Title, e.Amount, e.ExpenseDate, e.DeletedAt, u.FullName AS DeletedByName
                    FROM Expenses e LEFT JOIN Users u ON u.UserId = e.DeletedBy
                    WHERE e.DeletedAt IS NOT NULL ORDER BY e.DeletedAt DESC",
            r => new DeletedItem {
                Kind = "expense", KindLabel = "Expense", Id = G.G<int>(r,"ExpenseId"),
                Title = ((G.G<string>(r,"ExpenseNo") ?? "") + " " + (G.G<string>(r,"Title") ?? "")).Trim(),
                Subtitle = $"Rs {G.G<decimal>(r,"Amount"):N0} - {G.G<DateTime>(r,"ExpenseDate"):dd MMM yyyy}",
                DeletedAt = G.G<DateTime?>(r,"DeletedAt"), DeletedByName = G.G<string>(r,"DeletedByName") });

    public void Restore(int expenseId)
        => db.ExecSql("UPDATE Expenses SET DeletedAt = NULL, DeletedBy = NULL WHERE ExpenseId = @Id", new() {{"@Id", expenseId}});

    static Expense MapExp(SqlDataReader r) => new() {
        ExpenseId=G.G<int>(r,"ExpenseId"), ExpenseNo=G.G<string>(r,"ExpenseNo")??"",
        CategoryId=G.G<int?>(r,"CategoryId"), CategoryName=G.G<string>(r,"CategoryName"),
        Title=G.G<string>(r,"Title")??"", Description=G.G<string>(r,"Description"),
        Amount=G.G<decimal>(r,"Amount"), ExpenseDate=G.G<DateTime>(r,"ExpenseDate"),
        PaymentMode=G.G<string>(r,"PaymentMode")??"Cash", BillNo=G.G<string>(r,"BillNo"),
        VendorName=G.G<string>(r,"VendorName"), EnteredByName=G.G<string>(r,"EnteredByName"),
        CreatedAt=G.G<DateTime>(r,"CreatedAt")
    };
}
