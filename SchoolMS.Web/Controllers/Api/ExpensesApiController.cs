using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/expenses")]
[ApiRequireStaff]
public class ExpensesApiController(ExpensesService svc, LookupService lookup) : ControllerBase
{
    [HttpGet]
    [ApiRequirePermission("expenses_view")]
    public IActionResult Index(int page = 1, string? search = null, int? category = null, int? month = null, int? year = null,
        string? ym = null, DateTime? from = null, DateTime? to = null, bool all = false)
    {
        var (f, t, mode) = ExpenseRange.Resolve(ym, from, to, all, month, year);
        var res = svc.Query(new ExpenseQuery { Search = search, CategoryId = category, From = f, To = t, Page = page, PageSize = 15 });
        var cats = lookup.GetExpCats().Select(c => new { c.CategoryId, c.CategoryName, c.IsActive }).ToList();
        cats.Add(new { CategoryId = ExpenseRange.SalaryCategoryId, CategoryName = "Teacher Salary", IsActive = true });
        return Ok(new {
            expenses = res.Items.Select(e => new {
                e.ExpenseId, e.ExpenseNo, e.CategoryId, e.CategoryName, e.Title, e.Description, e.Amount, e.ExpenseDate, e.PaymentMode,
                e.BillNo, e.VendorName, e.EnteredByName, e.CreatedAt, source = e.Source,
            }),
            total = res.Total, page, totalPages = Math.Max(1, (int)Math.Ceiling((double)res.Total / 15)),
            totalAmount = res.TotalAmount, byCategory = res.ByCategory,
            from = f, to = t, mode,
            categories = cats, years = lookup.GetYears(),
        });
    }

    [HttpPost("save")]
    [ApiRequirePermission("expenses", requireEdit: true)]
    public IActionResult Save([FromBody] Expense model)
    {
        int uid = User.UserId() ?? 1;
        svc.Save(model, uid);
        return Ok(new { success = true });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("expenses", requireEdit: true)]
    public IActionResult Delete(int id)
    {
        int uid = User.UserId() ?? 1;
        svc.Delete(id, uid);
        return Ok(new { success = true });
    }
}
