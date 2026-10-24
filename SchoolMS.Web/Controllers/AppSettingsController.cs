using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// App Settings (admin only): the maintenance-mode switch for the mobile app. While it is on, every phone shows an
/// "under maintenance" page with this message and the API refuses requests (see Program.cs); the website keeps working.
/// </summary>
[RequireLogin]
public class AppSettingsController(MaintenanceService maintenance) : Controller
{
    bool IsAdmin => HttpContext.Session.GetInt32("RoleId") == 1;

    public IActionResult Index()
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Home");
        return View(maintenance.Get());
    }

    [HttpPost]
    public IActionResult Save(bool enabled, string? message)
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Home");
        var (ok, error) = maintenance.Set(enabled, message, HttpContext.Session.GetUserId() ?? 0);
        if (ok) TempData["Success"] = enabled ? "Maintenance mode is ON - the mobile app is blocked until you turn it off." : "Maintenance mode is OFF - the mobile app works normally.";
        else TempData["Error"] = error;
        return RedirectToAction("Index");
    }
}
