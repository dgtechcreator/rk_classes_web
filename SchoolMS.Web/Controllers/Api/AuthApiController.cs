using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Domain;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController(
    AuthService auth,
    UserMgmtService userMgmt,
    ParentService parentSvc,
    JwtTokenService jwt,
    UserAccessCache access,
    StaffNameService names) : ControllerBase
{
    public record StaffLoginReq(string Username, string Password);
    public record ParentLoginReq(string Phone, string Password);
    public record ParentRegisterReq(string Phone, string Password, string? FullName);

    [HttpPost("staff/login")]
    public IActionResult StaffLogin(StaffLoginReq req)
    {
        var (ok, u, msg) = auth.Login(req.Username, req.Password);
        if (!ok) return Unauthorized(new { error = msg });

        access.Invalidate(u!.UserId);
        var acc = access.Get(u.UserId);
        if (acc != null && !acc.CanUse)
            return Unauthorized(new { error = acc.CrBlocked ? "Your Class Representative access is switched off. Please contact the admin." : "This account is disabled. Please contact the admin." });

        var perms = acc is { Known: true } ? acc.Perms : userMgmt.GetPermissions(u.UserId);
        var token = jwt.GenerateStaffToken(u, perms);
        return Ok(new {
            token,
            user = new { u.UserId, u.FullName, DisplayName = SafeName(u.UserId, u.FullName), u.Username, u.RoleId, u.RoleName, u.Email, u.Phone, ProfilePicPath = acc?.ProfilePicPath ?? PicOf(u.UserId) },
            permissions = perms,
            classRep = CrJson(acc?.Cr),
        });
    }

    /// <summary>Fresh rights for the signed-in staff user - the app calls this on start / resume so a changed right shows up without a re-login.</summary>
    [HttpGet("me")]
    [ApiRequireStaff]
    public IActionResult Me()
    {
        var uid = User.UserId() ?? 0;
        var acc = access.Get(uid);
        var u = userMgmt.GetById(uid);
        if (acc == null || u == null || !acc.CanUse) return Unauthorized(new { error = "Account disabled." });
        return Ok(new {
            user = new { u.UserId, u.FullName, DisplayName = SafeName(u.UserId, u.FullName), u.Username, RoleId = acc.RoleId, RoleName = acc.RoleName, u.Email, u.Phone, ProfilePicPath = acc.ProfilePicPath ?? u.ProfilePicPath },
            permissions = acc.Known ? acc.Perms : userMgmt.GetPermissions(uid),
            classRep = CrJson(acc.Cr),
        });
    }

    string? PicOf(int userId)
    {
        try { return userMgmt.GetById(userId)?.ProfilePicPath; }
        catch { return null; }
    }

    string SafeName(int userId, string fallback)
    {
        try { var n = names.DisplayName(userId); return string.IsNullOrWhiteSpace(n) ? fallback : n; }
        catch { return fallback; }
    }

    static object? CrJson(CrScope? c) => c == null ? null : new {
        c.StudentId, c.StudentName, c.ClassId, c.ClassName, c.SectionId, c.SectionName,
        BatchId = c.EffectiveBatchId, BatchName = c.LimitToBatch ? c.BatchName : null, EditDays = CrScope.EditDays, MarksEditDays = CrScope.MarksEditDays,
    };

    [HttpPost("parent/login")]
    public IActionResult ParentLogin(ParentLoginReq req)
    {
        var (ok, p, msg) = parentSvc.Login(req.Phone, req.Password);
        if (!ok) return Unauthorized(new { error = msg });

        var token = jwt.GenerateParentToken(p!);
        return Ok(new { token, parent = new { p!.ParentId, p.Phone, p.FullName } });
    }

    [HttpPost("parent/register")]
    public IActionResult ParentRegister(ParentRegisterReq req)
    {
        var (ok, p, msg) = parentSvc.Register(req.Phone, req.Password, req.FullName);
        if (!ok) return BadRequest(new { error = msg });

        var token = jwt.GenerateParentToken(p!);
        return Ok(new { token, parent = new { p!.ParentId, p.Phone, p.FullName } });
    }
}
