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
        ViewBag.IsAdmin = true;
        ViewBag.Roles   = svc.GetRoles();
        ViewBag.Modules = svc.GetAllModules();
        return View("Form", new AppUser());
    }

    public IActionResult Edit(int id)
    {
        int loggedInUserId = HttpContext.Session.GetUserId() ?? 0;
        int loggedInRoleId = HttpContext.Session.GetInt32("RoleId") ?? 0;

        // Only Admin can edit others, regular users can edit only themselves
        if (loggedInRoleId != 1 && loggedInUserId != id)
            return RedirectToAction("AccessDenied", "Home");

        var user = svc.GetById(id);
        if (user == null) return NotFound();

        ViewBag.IsOwnProfile = (loggedInUserId == id);
        ViewBag.IsAdmin = (loggedInRoleId == 1);
        ViewBag.Roles   = svc.GetRoles();
        ViewBag.Modules = svc.GetAllModules();
        return View("Form", user);
    }

    [HttpPost]
    public IActionResult Save(AppUser model, string? newPassword)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        int loggedInRoleId = HttpContext.Session.GetInt32("RoleId") ?? 0;

        // Non-admin users can only edit their own profile
        if (loggedInRoleId != 1 && uid != model.UserId)
            return RedirectToAction("AccessDenied", "Home");

        if (!string.IsNullOrWhiteSpace(newPassword))
            model.PasswordHash = newPassword;
        var id = svc.Save(model, uid);
        if (id == -1) { TempData["Error"] = $"Username '{model.Username}' already exists."; return RedirectToAction("Create"); }
        TempData["Success"] = "User saved successfully.";

        // Admin goes to permissions, regular users go back to index
        if (loggedInRoleId == 1)
            return RedirectToAction("Permissions", new { id });
        else
            return RedirectToAction("Index");
    }

    public IActionResult AssignRights()
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return RedirectToAction("AccessDenied","Home");
        ViewBag.Users = svc.GetAll();
        ViewBag.AllModules = svc.GetAllModules();
        return View();
    }

    [HttpGet]
    public IActionResult GetUserRights(int userId)
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return Unauthorized();
        var user = svc.GetById(userId);
        if (user == null) return NotFound();
        var allModules = svc.GetAllModules();

        return Json(new {
            userId = user.UserId,
            userName = user.FullName,
            modules = allModules.Select(m => new {
                moduleId = m.ModuleId,
                moduleName = m.ModuleName,
                moduleKey = m.ModuleKey,
                icon = m.Icon,
                groupName = m.GroupName,
                orderNo = m.OrderNo,
                canView = user.Permissions.FirstOrDefault(p => p.ModuleId == m.ModuleId)?.CanView ?? false,
                canEdit = user.Permissions.FirstOrDefault(p => p.ModuleId == m.ModuleId)?.CanEdit ?? false
            }).ToList()
        });
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
    public IActionResult SavePermissions(int id, int userId, [FromBody] List<PermEntry> entries)
    {
        try {
            if (HttpContext.Session.GetInt32("RoleId") != 1) return Unauthorized();
            // The Permissions page posts to /Users/SavePermissions/{id} (route value "id"), other callers
            // may send ?userId= — accept either, otherwise the user id silently binds to 0.
            if (userId <= 0) userId = id;
            if (userId <= 0) return Json(new { success = false, message = "Invalid user." });
            if (entries == null) return Json(new { success = false, message = "No permissions received." });

            var user = svc.GetById(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });

            foreach (var e in entries)
                svc.SavePermission(userId, e.ModuleId, e.CanView, e.CanEdit);
            return Json(new { success = true, message = "Permissions saved." });
        } catch (Exception ex) {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
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
