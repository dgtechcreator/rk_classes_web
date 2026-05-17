using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolMS.Domain;
using System.Text.Json;

namespace SchoolMS.Web.Filters;

// ── Require Login ─────────────────────────────────────────────
public class RequireLoginAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (ctx.HttpContext.Session.GetInt32("UserId") == null)
            ctx.Result = new RedirectToActionResult("Login", "Account",
                new { returnUrl = ctx.HttpContext.Request.Path });
    }
}

// ── Require Permission ─────────────────────────────────────────
public class RequirePermissionAttribute(string moduleKey, bool requireEdit = false) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (ctx.HttpContext.Session.GetInt32("RoleId") == 1) return; // Admin bypass
        var perms = Sess.GetPermissions(ctx.HttpContext.Session);
        bool ok = requireEdit
            ? perms.TryGetValue(moduleKey + "_edit", out var e) && e
            : perms.TryGetValue(moduleKey, out var v) && v;
        if (!ok) ctx.Result = new RedirectToActionResult("AccessDenied", "Home", null);
    }
}

// ── Session helpers ────────────────────────────────────────────
public static class Sess
{
    public static void SetUser(this ISession s, User u)
    {
        s.SetInt32("UserId",    u.UserId);
        s.SetInt32("RoleId",    u.RoleId);
        s.SetString("Username", u.Username);
        s.SetString("FullName", u.FullName);
        s.SetString("RoleName", u.RoleName);
    }
    public static void SetPermissions(this ISession s, Dictionary<string,bool> p)
        => s.SetString("Perms", JsonSerializer.Serialize(p));
    public static Dictionary<string,bool> GetPermissions(this ISession s)
    {
        var j = s.GetString("Perms");
        if (string.IsNullOrEmpty(j)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string,bool>>(j) ?? new(); } catch { return new(); }
    }
    public static bool HasPerm(this ISession s, string key)
    {
        if (s.GetInt32("RoleId")==1) return true;
        var p = GetPermissions(s);
        return p.TryGetValue(key, out var v) && v;
    }
    public static bool HasEditPerm(this ISession s, string key)
    {
        if (s.GetInt32("RoleId")==1) return true;
        var p = GetPermissions(s);
        return p.TryGetValue(key+"_edit", out var v) && v;
    }
    public static int?    GetUserId(this ISession s)   => s.GetInt32("UserId");
    public static string? GetFullName(this ISession s) => s.GetString("FullName");
    public static string? GetRoleName(this ISession s) => s.GetString("RoleName");
}
