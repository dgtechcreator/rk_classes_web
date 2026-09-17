using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController(
    AuthService auth,
    UserMgmtService userMgmt,
    ParentService parentSvc,
    JwtTokenService jwt) : ControllerBase
{
    public record StaffLoginReq(string Username, string Password);
    public record ParentLoginReq(string Phone, string Password);
    public record ParentRegisterReq(string Phone, string Password, string? FullName);

    [HttpPost("staff/login")]
    public IActionResult StaffLogin(StaffLoginReq req)
    {
        var (ok, u, msg) = auth.Login(req.Username, req.Password);
        if (!ok) return Unauthorized(new { error = msg });

        var perms = userMgmt.GetPermissions(u!.UserId);
        var token = jwt.GenerateStaffToken(u, perms);
        return Ok(new {
            token,
            user = new { u.UserId, u.FullName, u.Username, u.RoleId, u.RoleName, u.Email, u.Phone },
            permissions = perms,
        });
    }

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
