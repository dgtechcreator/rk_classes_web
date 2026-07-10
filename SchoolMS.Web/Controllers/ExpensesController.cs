using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class ExpensesController(ExpensesService svc, LookupService lookup) : Controller
{
    public IActionResult Index(int page=1, string? search=null, int? category=null, int? month=null, int? year=null)
    {
        var (data, total) = svc.GetAll(page, 15, search, category, month, year);
        return View(new ExpenseListVM {
            Expenses=data, Total=total, Page=page, Search=search,
            CategoryFilter=category, MonthFilter=month, YearFilter=year,
            Categories=lookup.GetExpCats(),
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
        return View("Form", new ExpenseFormVM {
            Expense=new Expense { ExpenseId=id, ExpenseDate=DateTime.Today },
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
        svc.Delete(id);
        TempData["Success"] = "Expense deleted successfully.";
        return RedirectToAction("Index");
    }
}
