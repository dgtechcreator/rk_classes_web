using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class ExpensesService(ExpensesRepo repo)
{
    public (List<Expense> data, int total) GetAll(int pg, int ps, string? s, int? cat, int? month, int? year)
        => repo.GetAll(pg, ps, s, cat, month, year);
    public ExpenseListResult Query(ExpenseQuery q) => repo.Query(q);
    public Expense? GetById(int id) => repo.GetById(id);
    public decimal SalaryBetween(DateTime from, DateTime to) => repo.SalaryBetween(from, to);
    public int Save(Expense e, int by) => repo.Save(e, by);
    public void Delete(int expenseId, int deletedBy) => repo.Delete(expenseId, deletedBy);
}
