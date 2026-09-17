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
public class UsersApiController(UserMgmtService svc) : ControllerBase
{
    public record SaveUserReq(int UserId, string FullName, string Username, string? Email, string? Phone,
        int RoleId, bool IsActive, string? NewPassword);
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
        return Ok(svc.GetRoles());
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

        return Ok(new { user, isOwnProfile = loggedInUserId == id, isAdmin = loggedInRoleId == 1,
            roles = svc.GetRoles(), modules = svc.GetAllModules() });
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
        if (!string.IsNullOrWhiteSpace(req.NewPassword))
            model.PasswordHash = req.NewPassword;

        var id = svc.Save(model, uid);
        if (id == -1) return BadRequest(new { error = $"Username '{req.Username}' already exists." });

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
        var allModules = svc.GetAllModules();

        return Ok(new {
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
        return Ok(new { user, modules = svc.GetAllModules() });
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

            foreach (var e in req.Entries)
                svc.SavePermission(req.UserId, e.ModuleId, e.CanView, e.CanEdit);
            return Ok(new { success = true, message = "Permissions saved." });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    [HttpPost("change-password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordReq req)
    {
        var (ok, msg) = svc.ChangePassword(req.UserId, req.OldPassword, req.NewPassword);
        return Ok(new { success = ok, message = msg });
    }

    [HttpPost("{id:int}/toggle-active")]
    public IActionResult ToggleActive(int id)
    {
        var user = svc.GetById(id);
        if (user != null) { user.IsActive = !user.IsActive; svc.Save(user, User.UserId() ?? 1); }
        return Ok(new { success = true });
    }
}
