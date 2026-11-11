using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class ExpensesController(ExpensesService svc, LookupService lookup) : Controller
{
    /// <summary>Expenses + paid teacher salary. Default = this month; ym=yyyy-MM opens another month, all=true everything, from / to a custom range.</summary>
    public IActionResult Index(int page=1, string? search=null, int? category=null, string? ym=null, DateTime? from=null, DateTime? to=null, bool all=false, int? month=null, int? year=null)
    {
        var (f, t, mode) = ExpenseRange.Resolve(ym, from, to, all, month, year);
        var res = svc.Query(new ExpenseQuery { Search = search, CategoryId = category, From = f, To = t, Page = page, PageSize = 15 });
        var cats = lookup.GetExpCats();
        cats.Add(new ExpenseCat { CategoryId = ExpenseRange.SalaryCategoryId, CategoryName = "Teacher Salary" });
        return View(new ExpenseListVM {
            Expenses=res.Items, Total=res.Total, Page=page, Search=search,
            CategoryFilter=category, From=f, To=t, Mode=mode,
            TotalAmount=res.TotalAmount, ByCategory=res.ByCategory,
            Categories=cats,
            Years=lookup.GetYears()
        });
    }

    public IActionResult Create() => View("Form", new ExpenseFormVM {
        Expense=new Expense { ExpenseDate=DateTime.Today },
        Categories=lookup.GetExpCats(),
        Years=lookup.GetYears()
    });

    public IActionResult Edit(int id)
    {
        // The saved expense is loaded (it used to open an empty form, and saving then wiped the record).
        var existing = svc.GetById(id);
        if (existing == null) { TempData["Error"] = "This expense no longer exists."; return RedirectToAction("Index"); }
        return View("Form", new ExpenseFormVM {
            Expense=existing,
            Categories=lookup.GetExpCats(),
            Years=lookup.GetYears()
        });
    }

    [HttpPost]
    public IActionResult Save(ExpenseFormVM model)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        svc.Save(model.Expense, uid);
        TempData["Success"] = "Expense recorded successfully.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        svc.Delete(id, uid);
        TempData["Success"] = "Expense deleted successfully.";
        return RedirectToAction("Index");
    }
}
