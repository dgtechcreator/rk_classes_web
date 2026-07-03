using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class DeletedController(StudentService studentSvc, FacultyService facultySvc, FeesService feesSvc) : Controller
{
    public IActionResult Index()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1)
            return RedirectToAction("AccessDenied", "Home");
        var (students, _) = studentSvc.GetAll(1, 10000, null, null, null, null, null, "Inactive");
        var (faculty, _)  = facultySvc.GetAll(null, "Inactive", null, 1, 10000);
        var receipts      = feesSvc.GetDeletedPayments();
        ViewBag.Students = students;
        ViewBag.Faculty  = faculty;
        ViewBag.Receipts = receipts;
        return View();
    }

    [HttpPost]
    public IActionResult RestoreStudent(int id)
    {
        studentSvc.Restore(id);
        TempData["Success"] = "Student restored to Active.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult RestoreFaculty(int id)
    {
        facultySvc.Restore(id);
        TempData["Success"] = "Faculty member restored to Active.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult RestoreReceipt(int id)
    {
        feesSvc.RestorePayment(id);
        TempData["Success"] = "Receipt restored successfully.";
        return RedirectToAction("Index");
    }
}
