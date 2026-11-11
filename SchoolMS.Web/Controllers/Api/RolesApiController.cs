using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Roles &amp; Access for the mobile app (admin only) - the same page as the website's Users -> Roles &amp; Access.</summary>
[ApiController]
[Route("api/roles")]
[ApiRequireAdmin]
public class RolesApiController(UserMgmtService svc, ClassRepService crSvc, UserAccessCache access,
    PortalAccessService portal, RoleTemplateService roleTemplates) : ControllerBase
{
    int Me => User.UserId() ?? 0;

    List<Role> TemplateRoles()
    {
        int? crRole = null;
        try { crRole = crSvc.RoleId(); } catch { }
        return svc.GetRoles().Where(r => r.RoleId != 1 && r.RoleId != crRole).ToList();
    }

    bool IsCr(int userId) { try { return crSvc.GetByUserId(userId) != null; } catch { return false; } }

    /// <summary>Portal sections (Student / Parent login) with on/off, plus the staff roles that can have default rights.</summary>
    [HttpGet]
    public IActionResult Index()
    {
        var hidden = portal.Hidden();
        var users = svc.GetAll();
        return Ok(new {
            portal = PortalAccessService.Sections.Select(s => new { s.Key, s.Label, s.Description, visible = !hidden.Contains(s.Key) }),
            roles = TemplateRoles().Select(r => new { r.RoleId, r.RoleName, userCount = users.Count(u => u.RoleId == r.RoleId && u.UserId != 1) }),
        });
    }

    public record PortalReq(List<string> Visible);

    [HttpPost("portal")]
    public IActionResult SavePortal([FromBody] PortalReq req)
    {
        var keep = (req.Visible ?? new()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hidden = PortalAccessService.Sections.Select(s => s.Key).Where(k => !keep.Contains(k)).ToList();
        var (ok, err) = portal.SetHidden(hidden, Me);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = err });
    }

    [HttpGet("{roleId:int}/template")]
    public IActionResult GetTemplate(int roleId)
    {
        var role = TemplateRoles().FirstOrDefault(r => r.RoleId == roleId);
        if (role == null) return NotFound(new { error = "Role not found." });
        var template = roleTemplates.Get(roleId);
        string source; Dictionary<int, (bool view, bool edit)> map;
        if (template != null) { map = template; source = "saved"; }
        else
        {
            // no template yet: start from the rights of the first user who has this role
            var first = svc.GetAll().Where(u => u.RoleId == roleId && u.UserId != 1).Select(u => svc.GetById(u.UserId))
                .FirstOrDefault(u => u != null && u.Permissions.Any(p => p.CanView || p.CanEdit));
            map = first == null ? new() : first.Permissions.Where(p => p.CanView || p.CanEdit).ToDictionary(p => p.ModuleId, p => (true, p.CanEdit));
            source = first == null ? "empty" : "user:" + first.FullName;
        }
        return Ok(new {
            roleId, roleName = role.RoleName, source,
            userCount = svc.GetAll().Count(u => u.RoleId == roleId && u.UserId != 1),
            modules = svc.GetAllModules().Select(m => new {
                m.ModuleId, m.ModuleName, m.ModuleKey, m.Icon, groupName = m.GroupName ?? "Other", m.OrderNo,
                canView = map.TryGetValue(m.ModuleId, out var x) && x.view, canEdit = map.TryGetValue(m.ModuleId, out var y) && y.edit,
            }),
        });
    }

    public record PermEntry(int ModuleId, bool CanView, bool CanEdit);
    public record TemplateReq(bool ApplyToExisting, List<PermEntry> Entries);

    [HttpPost("{roleId:int}/template")]
    public IActionResult SaveTemplate(int roleId, [FromBody] TemplateReq req)
    {
        var role = TemplateRoles().FirstOrDefault(r => r.RoleId == roleId);
        if (role == null) return NotFound(new { error = "This role cannot be changed here." });
        var (ok, err) = roleTemplates.Save(roleId, (req.Entries ?? new()).Select(e => (e.ModuleId, e.CanView || e.CanEdit, e.CanEdit)), Me);
        if (!ok) return BadRequest(new { error = err });
        int applied = 0;
        if (req.ApplyToExisting)
        {
            var ids = roleTemplates.ApplyToRole(roleId, u => IsCr(u.UserId) || u.UserId == Me);
            foreach (var id in ids) access.Invalidate(id);
            applied = ids.Count;
        }
        return Ok(new { success = true, applied });
    }
}
