using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// A logged-in teacher's own payment history. Only the payments of the faculty profile linked to the caller
/// are ever loaded — there is no id parameter.
[RequireLogin]
public class MyPaymentsController(TeacherPaymentService paymentSvc, TeacherAccountLinker linker) : Controller
{
    [HttpGet]
    public IActionResult Index(int year = 0)
    {
        var faculty = linker.FindFaculty(HttpContext.Session.GetInt32("UserId") ?? 0);
        ViewBag.Linked = faculty != null;
        if (faculty == null) return View();

        var all = paymentSvc.Search(null, null, faculty.FacultyId, null);
        var list = year > 0 ? all.Where(p => p.PaymentYear == year).ToList() : all;
        ViewBag.FacultyName = faculty.FullName.Trim();
        ViewBag.Year = year;
        ViewBag.Years = all.Select(p => p.PaymentYear).Distinct().OrderByDescending(v => v).ToList();
        ViewBag.Summary = paymentSvc.Summarize(list);
        ViewBag.Payments = list;
        return View();
    }
}
