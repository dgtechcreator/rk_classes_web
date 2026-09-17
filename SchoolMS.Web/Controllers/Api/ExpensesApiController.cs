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
    public IActionResult Index(int page = 1, string? search = null, int? category = null, int? month = null, int? year = null)
    {
        var (data, total) = svc.GetAll(page, 15, search, category, month, year);
        return Ok(new {
            expenses = data, total, page, totalPages = Math.Max(1, (int)Math.Ceiling((double)total / 15)),
            categories = lookup.GetExpCats(), years = lookup.GetYears(),
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
