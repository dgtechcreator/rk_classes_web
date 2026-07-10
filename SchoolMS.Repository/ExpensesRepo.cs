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
    public int Save(Expense e, int by) => db.ExecOut("sp_SaveExpense", new() {
        { "@ExpenseId",e.ExpenseId }, { "@CategoryId",e.CategoryId }, { "@Title",e.Title },
        { "@Description",e.Description }, { "@Amount",e.Amount }, { "@ExpenseDate",e.ExpenseDate },
        { "@PaymentMode",e.PaymentMode }, { "@BillNo",e.BillNo }, { "@VendorName",e.VendorName },
        { "@EnteredBy",by }
    }, "@NewExpenseId");
    public void Delete(int expenseId, int deletedBy) => db.Exec("sp_DeleteExpense", new() { { "@ExpenseId",expenseId }, { "@DeletedBy",deletedBy } });
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
