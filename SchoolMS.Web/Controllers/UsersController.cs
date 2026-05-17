using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class UsersController(UserMgmtService svc) : Controller
{
    public IActionResult Index()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1)
            return RedirectToAction("AccessDenied", "Home");
        return View(svc.GetAll());
    }

    public IActionResult Create()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return RedirectToAction("AccessDenied","Home");
        ViewBag.Roles   = svc.GetRoles();
        ViewBag.Modules = svc.GetAllModules();
        return View("Form", new AppUser());
    }

    public IActionResult Edit(int id)
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return RedirectToAction("AccessDenied","Home");
        var user = svc.GetById(id);
        if (user == null) return NotFound();
        ViewBag.Roles   = svc.GetRoles();
        ViewBag.Modules = svc.GetAllModules();
        return View("Form", user);
    }

    [HttpPost]
    public IActionResult Save(AppUser model, string? newPassword)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        if (!string.IsNullOrWhiteSpace(newPassword))
            model.PasswordHash = newPassword;
        var id = svc.Save(model, uid);
        if (id == -1) { TempData["Error"] = $"Username '{model.Username}' already exists."; return RedirectToAction("Create"); }
        TempData["Success"] = "User saved successfully.";
        return RedirectToAction("Permissions", new { id });
    }

    public IActionResult Permissions(int id)
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return RedirectToAction("AccessDenied","Home");
        var user = svc.GetById(id);
        if (user == null) return NotFound();
        ViewBag.Modules = svc.GetAllModules();
        return View(user);
    }

    [HttpPost]
    public IActionResult SavePermissions(int userId, [FromBody] List<PermEntry> entries)
    {
        foreach (var e in entries)
            svc.SavePermission(userId, e.ModuleId, e.CanView, e.CanEdit);
        return Json(new { success = true, message = "Permissions saved." });
    }

    [HttpPost]
    public IActionResult ChangePassword(int userId, string oldPassword, string newPassword)
    {
        var (ok, msg) = svc.ChangePassword(userId, oldPassword, newPassword);
        TempData[ok?"Success":"Error"] = msg;
        return RedirectToAction("Edit", new { id = userId });
    }

    [HttpPost]
    public IActionResult ToggleActive(int id)
    {
        var user = svc.GetById(id);
        if (user != null) { user.IsActive = !user.IsActive; svc.Save(user, HttpContext.Session.GetUserId()??1); }
        TempData["Success"] = "User status updated.";
        return RedirectToAction("Index");
    }
}

public class PermEntry { public int ModuleId{get;set;} public bool CanView{get;set;} public bool CanEdit{get;set;} }
