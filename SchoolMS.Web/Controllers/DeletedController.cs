using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class DeletedController(StudentService studentSvc, FacultyService facultySvc, FeesService feesSvc, UserMgmtService userSvc, UserAccessCache access, DeletedRecordsService othersSvc) : Controller
{
    public IActionResult Index()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1)
            return RedirectToAction("AccessDenied", "Home");
        var (students, _) = studentSvc.GetAll(1, 10000, null, null, null, null, null, "Inactive");
        var (faculty, _)  = facultySvc.GetAll(null, "Inactive", null, 1, 10000);
        var receipts      = feesSvc.GetDeletedPayments();
        othersSvc.Enrich(students, faculty, studentSvc, facultySvc);
        ViewBag.Students = students;
        ViewBag.Faculty  = faculty;
        ViewBag.Receipts = receipts;
        ViewBag.Users    = userSvc.GetDeleted();
        ViewBag.Others   = othersSvc.GetOthers();
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
    public IActionResult RestoreUser(int id)
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return RedirectToAction("AccessDenied", "Home");
        userSvc.Restore(id);
        access.Invalidate(id);
        TempData["Success"] = "User restored and set to Active.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult RestoreOther(string kind, int id)
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return RedirectToAction("AccessDenied", "Home");
        var ok = othersSvc.Restore(kind, id);
        TempData[ok ? "Success" : "Error"] = ok ? "Record restored." : "Unknown record type.";
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
