using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class UsersController(UserMgmtService svc, ClassRepService crSvc, UserAccessCache access, ProfilePhotoStore photos,
    FacultyService facultySvc, TeacherAccountLinker linker) : Controller
{
    bool IsAdmin => HttpContext.Session.GetInt32("RoleId") == 1;
    int Me => HttpContext.Session.GetUserId() ?? 0;

    /// <summary>Class Representative record of a user (null for normal users, or before SQL/61 has been run).</summary>
    ClassRep? CrOf(int userId)
    {
        try { return crSvc.GetByUserId(userId); }
        catch { return null; }
    }

    /// <summary>Roles offered in the dropdown. "Class Representative" is created only from the Class Representatives page.</summary>
    List<Role> RolesFor(AppUser? user)
    {
        var crRole = SafeCrRoleId();
        return svc.GetRoles().Where(r => r.RoleId != crRole || (user != null && user.RoleId == crRole)).ToList();
    }

    int? SafeCrRoleId()
    {
        try { return crSvc.RoleId(); }
        catch { return null; }
    }

    public IActionResult Index()
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Home");
        List<ClassRep> reps;
        try { reps = crSvc.GetAll(); } catch { reps = new(); }
        ViewBag.CrByUser = reps.GroupBy(c => c.UserId).ToDictionary(g => g.Key, g => g.First());
        return View(svc.GetAll());
    }

    public IActionResult Create()
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied","Home");
        ViewBag.IsAdmin = true;
        FillFacultyOptions(0);
        ViewBag.Roles   = RolesFor(null);
        ViewBag.Modules = svc.GetAllModules();
        return View("Form", new AppUser());
    }

    public IActionResult Edit(int id)
    {
        // Only Admin can edit others, regular users can edit only themselves
        if (!IsAdmin && Me != id)
            return RedirectToAction("AccessDenied", "Home");

        var user = svc.GetById(id);
        if (user == null) return NotFound();

        if (IsAdmin) FillFacultyOptions(id);
        ViewBag.IsOwnProfile = (Me == id);
        ViewBag.IsAdmin = IsAdmin;
        ViewBag.Roles   = RolesFor(user);
        ViewBag.Modules = svc.GetAllModules();
        ViewBag.Cr      = CrOf(id);
        return View("Form", user);
    }

    /// <summary>Faculty profiles offered in the "Linked Faculty profile" dropdown, and what automatic matching would pick today.</summary>
    void FillFacultyOptions(int userId)
    {
        try
        {
            ViewBag.Faculties = facultySvc.GetAllActive();
            ViewBag.AutoFaculty = userId > 0 ? linker.AutoMatch(userId) : null;
        }
        catch { ViewBag.Faculties = new List<Faculty>(); }
    }

    /// <summary>Stores / removes the login's profile photo. A bad photo never blocks the rest of the user save - it only shows a message.</summary>
    void ApplyPhoto(int userId, IFormFile? file, bool remove)
    {
        bool upload = file != null && file.Length > 0;
        if (!upload && !remove) return;
        try
        {
            var old = svc.GetById(userId)?.ProfilePicPath;
            if (upload)
            {
                var (path, err) = photos.Save(file, "users");
                if (err != null) { TempData["Error"] = err; return; }
                try { svc.SetPhoto(userId, path); }
                catch (Microsoft.Data.SqlClient.SqlException) { photos.Delete(path); TempData["Error"] = "Profile photo is not enabled on the server yet (run SQL/65_UserProfilePhoto.sql)."; return; }
                photos.Delete(old);
            }
            else
            {
                svc.SetPhoto(userId, null);
                photos.Delete(old);
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException) { TempData["Error"] = "Profile photo is not enabled on the server yet (run SQL/65_UserProfilePhoto.sql)."; }
    }

    [HttpPost]
    public IActionResult Save(AppUser model, string? newPassword, IFormFile? ProfilePic, bool RemovePhoto = false)
    {
        int uid = Me;
        bool isAdmin = IsAdmin;

        // Non-admin users can only edit their own profile
        if (!isAdmin && uid != model.UserId)
            return RedirectToAction("AccessDenied", "Home");

        if (model.UserId > 0)
        {
            var existing = svc.GetById(model.UserId);
            if (existing == null) return NotFound();

            if (!isAdmin)
            {
                // A normal user may change his own name only. Role, status, e-mail, phone and password rights
                // always come from the database - never from the posted form (otherwise anyone could post RoleId=1).
                model.RoleId = existing.RoleId;
                model.IsActive = existing.IsActive;
                model.Email = existing.Email;
                model.Phone = existing.Phone;
                if (CrOf(existing.UserId) != null) model.FullName = existing.FullName;   // a CR's name is the student's name (shown as "Added by")
                newPassword = null;
            }
            else
            {
                if (existing.UserId == 1) { model.RoleId = existing.RoleId; model.IsActive = true; }   // Super Admin stays admin + active
                else if (existing.UserId == uid) model.IsActive = true;                                  // an admin cannot lock himself out
                var crRole = SafeCrRoleId();
                if (CrOf(existing.UserId) != null) model.RoleId = existing.RoleId;                       // a CR stays a CR (manage on Class Representatives page)
                else if (crRole != null && model.RoleId == crRole)
                {
                    TempData["Error"] = "Use the Class Representatives page to make a student a CR.";
                    return RedirectToAction("Edit", new { id = model.UserId });
                }
            }
            model.Username = existing.Username;   // username can never change after creation
        }
        else
        {
            if (!isAdmin) return RedirectToAction("AccessDenied", "Home");
            var crRole = SafeCrRoleId();
            if (crRole != null && model.RoleId == crRole)
            {
                TempData["Error"] = "Use the Class Representatives page to make a student a CR.";
                return RedirectToAction("Create");
            }
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
            model.PasswordHash = newPassword;
        var id = svc.Save(model, uid);
        if (id == -1) { TempData["Error"] = $"Username '{model.Username}' already exists."; return RedirectToAction("Create"); }
        if (isAdmin) svc.SetGender(id, model.Gender);   // Sir / Ma'am greeting; only the admin sets it
        ApplyPhoto(id, ProfilePic, RemovePhoto);
        if (isAdmin && Request.HasFormContentType && Request.Form.ContainsKey("FacultyId"))   // "" = automatic matching
        {
            var (linkOk, linkErr) = svc.LinkFaculty(id, model.FacultyId);
            if (!linkOk) TempData["Error"] = linkErr;
        }
        access.Invalidate(id);
        TempData["Success"] = "User saved successfully.";

        // Admin goes to permissions, regular users go back to index
        if (isAdmin)
            return RedirectToAction("Permissions", new { id });
        else
            return RedirectToAction("Index", "Home");
    }

    public IActionResult AssignRights()
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied","Home");
        ViewBag.Users = svc.GetAll();
        ViewBag.AllModules = svc.GetAllModules();
        return View();
    }

    [HttpGet]
    public IActionResult GetUserRights(int userId)
    {
        if (!IsAdmin) return Unauthorized();
        var user = svc.GetById(userId);
        if (user == null) return NotFound();
        var cr = CrOf(userId) != null;
        var allModules = svc.GetAllModules().Where(m => !cr || ClassRepService.AllowedModuleKeys.Contains(m.ModuleKey));

        return Json(new {
            userId = user.UserId,
            userName = user.FullName,
            isClassRep = cr,
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
        if (!IsAdmin) return RedirectToAction("AccessDenied","Home");
        var user = svc.GetById(id);
        if (user == null) return NotFound();
        var cr = CrOf(id);
        if (cr != null)   // a CR can hold only the fixed CR modules
            user.Permissions = user.Permissions.Where(m => ClassRepService.AllowedModuleKeys.Contains(m.ModuleKey)).ToList();
        ViewBag.Modules = svc.GetAllModules();
        ViewBag.Cr = cr;
        return View(user);
    }

    [HttpPost]
    public IActionResult SavePermissions(int id, int userId, [FromBody] List<PermEntry> entries)
    {
        try {
            if (!IsAdmin) return Unauthorized();
            // The Permissions page posts to /Users/SavePermissions/{id} (route value "id"), other callers
            // may send ?userId= — accept either, otherwise the user id silently binds to 0.
            if (userId <= 0) userId = id;
            if (userId <= 0) return Json(new { success = false, message = "Invalid user." });
            if (entries == null) return Json(new { success = false, message = "No permissions received." });

            var user = svc.GetById(userId);
            if (user == null) return Json(new { success = false, message = "User not found." });
            if (userId == 1) return Json(new { success = false, message = "Super Admin always has full access - nothing to change." });

            var error = ApplyPermissions(userId, entries);
            if (error != null) return Json(new { success = false, message = error });
            return Json(new { success = true, message = "Permissions saved. They apply to the user immediately." });
        } catch (Exception ex) {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>Validates and stores the rights. Returns an error text, or null when saved.</summary>
    string? ApplyPermissions(int userId, List<PermEntry> entries)
    {
        var modules = svc.GetAllModules().ToDictionary(m => m.ModuleId);
        var isCr = CrOf(userId) != null;
        foreach (var e in entries)
        {
            if (!modules.TryGetValue(e.ModuleId, out var m)) return "Unknown module in the request.";
            if (isCr && (e.CanView || e.CanEdit) && !ClassRepService.AllowedModuleKeys.Contains(m.ModuleKey))
                return $"A Class Representative cannot be given '{m.ModuleName}'.";
        }
        foreach (var e in entries.Where(e => modules.ContainsKey(e.ModuleId)))
            svc.SavePermission(userId, e.ModuleId, e.CanView || e.CanEdit, e.CanEdit);   // edit always includes view
        access.Invalidate(userId);
        return null;
    }

    [HttpPost]
    public IActionResult ChangePassword(int userId, string oldPassword, string newPassword)
    {
        // Own password only (admins reset others from the Edit form).
        if (userId != Me && !IsAdmin) return RedirectToAction("AccessDenied", "Home");
        var (ok, msg) = svc.ChangePassword(userId, oldPassword, newPassword);
        TempData[ok?"Success":"Error"] = msg;
        return RedirectToAction("Edit", new { id = userId });
    }

    // Soft delete (admin only). The Super Admin and the logged-in admin themself cannot be deleted.
    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Home");
        if (id == 1 || id == Me)
        {
            TempData["Error"] = id == 1 ? "Super Admin cannot be deleted." : "You cannot delete your own account.";
            return RedirectToAction("Index");
        }
        var user = svc.GetById(id);
        if (user == null) return NotFound();
        svc.Delete(id, Me);
        access.Invalidate(id);
        TempData["Success"] = $"{user.FullName} deleted. You can restore them from Deleted Records.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult ToggleActive(int id)
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Home");
        if (id == 1 || id == Me)
        {
            TempData["Error"] = id == 1 ? "Super Admin cannot be deactivated." : "You cannot deactivate your own account.";
            return RedirectToAction("Index");
        }
        var user = svc.GetById(id);
        if (user != null) { user.IsActive = !user.IsActive; svc.Save(user, Me); access.Invalidate(id); }
        TempData["Success"] = "User status updated.";
        return RedirectToAction("Index");
    }
}

public class PermEntry { public int ModuleId{get;set;} public bool CanView{get;set;} public bool CanEdit{get;set;} }
