using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// <summary>Send Notification (admin only): a message to everybody, a group, or chosen students / staff.</summary>
[RequireLogin]
public class NotifyController(BroadcastService svc, LookupService lookup) : Controller
{
    int Uid => HttpContext.Session.GetUserId() ?? 0;
    bool IsAdmin => HttpContext.Session.GetInt32("RoleId") == 1;

    public IActionResult Index()
    {
        if (!IsAdmin) return RedirectToAction("Index", "Home");
        ViewBag.Classes = lookup.GetClasses().Where(c => c.IsActive).ToList();
        ViewBag.Sections = lookup.GetSections().Where(c => c.IsActive).ToList();
        ViewBag.Batches = lookup.GetBatches().Where(c => c.IsActive).ToList();
        ViewBag.Staff = svc.StaffList(Uid);
        ViewData["Title"] = "Send Notification";
        return View();
    }

    [HttpPost]
    public IActionResult Send([FromBody] BroadcastReq req)
    {
        if (!IsAdmin) return Json(new { success = false, error = "Only the admin can send notifications." });
        var (error, students, staff) = svc.Send(req, Uid);
        return Json(new { success = error == null, error, students, staff });
    }

    [HttpGet]
    public IActionResult Students(string? q)
        => !IsAdmin ? Json(new { students = Array.Empty<object>() }) : Json(new { students = svc.FindStudents(q).Select(s => new { id = s.id, label = s.label }) });
}
