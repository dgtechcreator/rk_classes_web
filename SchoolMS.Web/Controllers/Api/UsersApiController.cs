using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

// Mirrors UsersController's own authorization logic exactly: most actions are admin-only
// (RoleId == 1) by direct check in the original controller, not by module permission —
// Save/ChangePassword/ToggleActive additionally allow a user to act on their own record.
[ApiController]
[Route("api/users")]
[ApiRequireStaff]
public class UsersApiController(UserMgmtService svc, ClassRepService crSvc, UserAccessCache access) : ControllerBase
{
    ClassRep? CrOf(int userId)
    {
        try { return crSvc.GetByUserId(userId); }
        catch { return null; }
    }

    int? SafeCrRoleId()
    {
        try { return crSvc.RoleId(); }
        catch { return null; }
    }

    public record SaveUserReq(int UserId, string FullName, string Username, string? Email, string? Phone,
        int RoleId, bool IsActive, string? NewPassword, string? Gender = null);
    public record SavePermissionsReq(int UserId, List<PermEntry> Entries);
    public record PermEntry(int ModuleId, bool CanView, bool CanEdit);
    public record ChangePasswordReq(int UserId, string OldPassword, string NewPassword);

    [HttpGet]
    public IActionResult Index()
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        return Ok(svc.GetAll());
    }

    // The MVC UsersController only ever needs the role list bundled inside a per-user page (ViewBag),
    // so there was never a standalone endpoint for it — the mobile app's "create user" form needs the
    // list before any user exists to look up, hence this addition.
    [HttpGet("roles")]
    public IActionResult Roles()
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        var crRole = SafeCrRoleId();
        return Ok(svc.GetRoles().Where(r => r.RoleId != crRole));   // CR logins are created from the Class Representatives screen
    }

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        int loggedInUserId = User.UserId() ?? 0;
        int loggedInRoleId = User.RoleId() ?? 0;
        if (loggedInRoleId != 1 && loggedInUserId != id)
            return StatusCode(403, new { error = "Access denied." });

        var user = svc.GetById(id);
        if (user == null) return NotFound();

        var crRole = SafeCrRoleId();
        return Ok(new { user, isOwnProfile = loggedInUserId == id, isAdmin = loggedInRoleId == 1,
            roles = svc.GetRoles().Where(r => r.RoleId != crRole || r.RoleId == user.RoleId),
            modules = svc.GetAllModules(), isClassRep = CrOf(id) != null });
    }

    [HttpPost("save")]
    public IActionResult Save([FromBody] SaveUserReq req)
    {
        int uid = User.UserId() ?? 1;
        int loggedInRoleId = User.RoleId() ?? 0;

        if (loggedInRoleId != 1 && uid != req.UserId)
            return StatusCode(403, new { error = "Access denied." });

        var model = new AppUser {
            UserId = req.UserId, FullName = req.FullName, Username = req.Username,
            Email = req.Email, Phone = req.Phone, RoleId = req.RoleId, IsActive = req.IsActive,
        };
        bool isAdmin = loggedInRoleId == 1;
        var crRole = SafeCrRoleId();

        if (req.UserId > 0)
        {
            var existing = svc.GetById(req.UserId);
            if (existing == null) return NotFound();
            if (!isAdmin)
            {
                // A normal user may change his own name only - role / status / contact / password come from the database,
                // never from the request (otherwise anyone could send RoleId = 1 for himself).
                model.RoleId = existing.RoleId; model.IsActive = existing.IsActive;
                model.Email = existing.Email; model.Phone = existing.Phone;
                model.PasswordHash = "";
                if (CrOf(existing.UserId) != null) model.FullName = existing.FullName;   // a CR's name is the student's name (shown as "Added by")
            }
            else
            {
                if (existing.UserId == 1) { model.RoleId = existing.RoleId; model.IsActive = true; }
                else if (existing.UserId == uid) model.IsActive = true;
                if (CrOf(existing.UserId) != null) model.RoleId = existing.RoleId;
                else if (crRole != null && model.RoleId == crRole)
                    return BadRequest(new { error = "Use the Class Representatives screen to make a student a CR." });
            }
            model.Username = existing.Username;
        }
        else
        {
            if (!isAdmin) return StatusCode(403, new { error = "Access denied." });
            if (crRole != null && model.RoleId == crRole)
                return BadRequest(new { error = "Use the Class Representatives screen to make a student a CR." });
        }

        if (isAdmin && !string.IsNullOrWhiteSpace(req.NewPassword))
            model.PasswordHash = req.NewPassword;

        var id = svc.Save(model, uid);
        if (id == -1) return BadRequest(new { error = $"Username '{req.Username}' already exists." });
        if (isAdmin && req.Gender != null) svc.SetGender(id, req.Gender);   // "" clears it
        access.Invalidate(id);

        return Ok(new { userId = id });
    }

    [HttpGet("assign-rights")]
    public IActionResult AssignRights()
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        return Ok(new { users = svc.GetAll(), allModules = svc.GetAllModules() });
    }

    [HttpGet("{userId:int}/rights")]
    public IActionResult GetUserRights(int userId)
    {
        if (User.RoleId() != 1) return Unauthorized();
        var user = svc.GetById(userId);
        if (user == null) return NotFound();
        var cr = CrOf(userId) != null;
        var allModules = svc.GetAllModules().Where(m => !cr || ClassRepService.AllowedModuleKeys.Contains(m.ModuleKey)).ToList();

        return Ok(new {
            isClassRep = cr,
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

    [HttpGet("{id:int}/permissions")]
    public IActionResult Permissions(int id)
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        var user = svc.GetById(id);
        if (user == null) return NotFound();
        var cr = CrOf(id) != null;
        return Ok(new { user, isClassRep = cr, modules = svc.GetAllModules().Where(m => !cr || ClassRepService.AllowedModuleKeys.Contains(m.ModuleKey)) });
    }

    [HttpPost("permissions/save")]
    public IActionResult SavePermissions([FromBody] SavePermissionsReq req)
    {
        try
        {
            if (User.RoleId() != 1) return Unauthorized();
            if (req.UserId <= 0) return Ok(new { success = false, message = "Invalid user." });

            var user = svc.GetById(req.UserId);
            if (user == null) return Ok(new { success = false, message = "User not found." });
            if (req.UserId == 1) return Ok(new { success = false, message = "Super Admin always has full access - nothing to change." });
            if (req.Entries == null) return Ok(new { success = false, message = "No permissions received." });

            var modules = svc.GetAllModules().ToDictionary(m => m.ModuleId);
            var isCr = CrOf(req.UserId) != null;
            foreach (var e in req.Entries)
            {
                if (!modules.TryGetValue(e.ModuleId, out var m)) return Ok(new { success = false, message = "Unknown module in the request." });
                if (isCr && (e.CanView || e.CanEdit) && !ClassRepService.AllowedModuleKeys.Contains(m.ModuleKey))
                    return Ok(new { success = false, message = $"A Class Representative cannot be given '{m.ModuleName}'." });
            }
            foreach (var e in req.Entries)
                svc.SavePermission(req.UserId, e.ModuleId, e.CanView || e.CanEdit, e.CanEdit);   // edit always includes view
            access.Invalidate(req.UserId);
            return Ok(new { success = true, message = "Permissions saved. They apply to the user immediately." });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    [HttpPost("change-password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordReq req)
    {
        if (req.UserId != User.UserId() && User.RoleId() != 1)
            return StatusCode(403, new { error = "Access denied." });
        var (ok, msg) = svc.ChangePassword(req.UserId, req.OldPassword, req.NewPassword);
        return Ok(new { success = ok, message = msg });
    }

    // Soft delete (admin only) - restore lives in DeletedApiController. Super Admin / self are protected.
    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        int me = User.UserId() ?? 0;
        if (id == 1) return BadRequest(new { error = "Super Admin cannot be deleted." });
        if (id == me) return BadRequest(new { error = "You cannot delete your own account." });
        if (svc.GetById(id) == null) return NotFound();
        svc.Delete(id, me);
        access.Invalidate(id);
        return Ok(new { success = true });
    }

    [HttpPost("{id:int}/toggle-active")]
    public IActionResult ToggleActive(int id)
    {
        if (User.RoleId() != 1) return StatusCode(403, new { error = "Admin only." });
        if (id == 1) return BadRequest(new { error = "Super Admin cannot be deactivated." });
        if (id == User.UserId()) return BadRequest(new { error = "You cannot deactivate your own account." });
        var user = svc.GetById(id);
        if (user != null) { user.IsActive = !user.IsActive; svc.Save(user, User.UserId() ?? 1); access.Invalidate(id); }
        return Ok(new { success = true });
    }
}
