using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SchoolMS.Web.Auth;

// Claim readers — mirror SchoolMS.Web.Filters.Sess but backed by JWT claims instead of session state.
public static class ApiClaims
{
    public static int? UserId(this ClaimsPrincipal u)
        => int.TryParse(u.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    public static string? UserType(this ClaimsPrincipal u) => u.FindFirst("type")?.Value;

    public static int? RoleId(this ClaimsPrincipal u)
        => int.TryParse(u.FindFirst("roleId")?.Value, out var id) ? id : null;

    public static string? Phone(this ClaimsPrincipal u) => u.FindFirst("phone")?.Value;

    public static Dictionary<string, bool> Perms(this ClaimsPrincipal u)
    {
        var j = u.FindFirst("perms")?.Value;
        if (string.IsNullOrEmpty(j)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(j) ?? new(); }
        catch { return new(); }
    }

    public static bool HasPerm(this ClaimsPrincipal u, string key)
        => u.RoleId() == 1 || (u.Perms().TryGetValue(key, out var v) && v);

    public static bool HasEditPerm(this ClaimsPrincipal u, string key)
        => u.RoleId() == 1 || (u.Perms().TryGetValue(key + "_edit", out var v) && v);
}

// ── Require staff login (any authenticated staff token) ────────────────────────
public class ApiRequireStaffAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.UserType() != "staff")
            ctx.Result = new UnauthorizedObjectResult(new { error = "Staff login required." });
    }
}

// ── Require parent login ────────────────────────────────────────────────────────
public class ApiRequireParentAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.UserType() != "parent")
            ctx.Result = new UnauthorizedObjectResult(new { error = "Parent login required." });
    }
}

// Either staff or parent — mirrors RequireAdminOrParentAttribute (e.g. fee receipts).
public class ApiRequireStaffOrParentAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.UserType() is not ("staff" or "parent"))
            ctx.Result = new UnauthorizedObjectResult(new { error = "Login required." });
    }
}

// ── Require module permission — mirrors RequirePermissionAttribute ─────────────
public class ApiRequirePermissionAttribute(string moduleKey, bool requireEdit = false) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.UserType() != "staff")
        { ctx.Result = new UnauthorizedObjectResult(new { error = "Staff login required." }); return; }

        bool ok = requireEdit ? u.HasEditPerm(moduleKey) : u.HasPerm(moduleKey);
        if (!ok) ctx.Result = new ObjectResult(new { error = "Access denied." }) { StatusCode = 403 };
    }
}

// ── Admin only (RoleId 1) — e.g. deleted-records trash/restore, intentionally stricter than the
// bare [RequireLogin] some MVC actions carry, since a JSON API is more easily scripted against than
// an HTML form; matches what those features are obviously meant to be admin-only for. ────────────
public class ApiRequireAdminAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.UserType() != "staff" || u.RoleId() != 1)
            ctx.Result = new ObjectResult(new { error = "Access denied." }) { StatusCode = 403 };
    }
}

// ── Teacher payment / attendance — permission-gated modules ────────────────────
public class ApiRequireTeacherPaymentAccessAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.UserType() != "staff")
        { ctx.Result = new UnauthorizedObjectResult(new { error = "Staff login required." }); return; }
        if (!u.HasPerm("teacher_payment"))
            ctx.Result = new ObjectResult(new { error = "Access denied." }) { StatusCode = 403 };
    }
}

public class ApiRequireTeacherAttendanceAccessAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext ctx)
    {
        var u = ctx.HttpContext.User;
        if (u.Identity?.IsAuthenticated != true || u.UserType() != "staff")
        { ctx.Result = new UnauthorizedObjectResult(new { error = "Staff login required." }); return; }
        if (!u.HasPerm("teacher_attendance"))
            ctx.Result = new ObjectResult(new { error = "Access denied." }) { StatusCode = 403 };
    }
}
