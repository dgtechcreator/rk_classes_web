using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace SchoolMS.Web.Filters;

// ── Require Login ─────────────────────────────────────────────────────────────
public class RequireLoginAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (ctx.HttpContext.Session.GetInt32("UserId") == null)
        {
            ctx.Result = new RedirectToActionResult("Login", "Account",
                new { returnUrl = ctx.HttpContext.Request.Path });
        }
    }
}

// ── Require Permission ────────────────────────────────────────────────────────
public class RequirePermissionAttribute(string moduleKey, bool requireEdit = false) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (ctx.HttpContext.Session.GetInt32("UserId") == null)
        {
            ctx.Result = new RedirectToActionResult("Login", "Account",
                new { returnUrl = ctx.HttpContext.Request.Path });
            return;
        }

        var roleId = ctx.HttpContext.Session.GetInt32("RoleId");
        if (roleId == 1) return; // Admin — full access

        var perms = Sess.GetPermissions(ctx.HttpContext.Session);
        if (!perms.Any())
        {
            ctx.Result = new RedirectToActionResult("Login", "Account",
                new { returnUrl = ctx.HttpContext.Request.Path });
            return;
        }

        bool ok = requireEdit
            ? perms.TryGetValue(moduleKey + "_edit", out var e) && e
            : perms.TryGetValue(moduleKey, out var v) && v;

        if (!ok) ctx.Result = new RedirectToActionResult("Index", "Dashboard",
            new { msg = "access_denied" });
    }
}

// ── Session helpers ───────────────────────────────────────────────────────────
public static class Sess
{
    public static void SetUser(this ISession s, SchoolMS.Domain.User u)
    {
        s.SetInt32("UserId",    u.UserId);
        s.SetInt32("RoleId",    u.RoleId);
        s.SetString("Username", u.Username);
        s.SetString("FullName", u.FullName);
        s.SetString("RoleName", u.RoleName);
    }

    public static void SetPermissions(this ISession s, Dictionary<string, bool> p)
        => s.SetString("Perms", JsonSerializer.Serialize(p));

    public static Dictionary<string, bool> GetPermissions(this ISession s)
    {
        var j = s.GetString("Perms");
        if (string.IsNullOrEmpty(j)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(j) ?? new(); }
        catch { return new(); }
    }

    public static bool HasPerm(this ISession s, string key)
    {
        if (s.GetInt32("RoleId") == 1) return true;
        return GetPermissions(s).TryGetValue(key, out var v) && v;
    }

    public static bool HasEditPerm(this ISession s, string key)
    {
        if (s.GetInt32("RoleId") == 1) return true;
        return GetPermissions(s).TryGetValue(key + "_edit", out var v) && v;
    }

    public static int?    GetUserId(this ISession s)   => s.GetInt32("UserId");
    public static string? GetFullName(this ISession s) => s.GetString("FullName");
    public static string? GetRoleName(this ISession s) => s.GetString("RoleName");

    // ── Parent session helpers ────────────────────────────────────
    public static void    SetParentId(this ISession s, int id)      => s.SetInt32("ParentId", id);
    public static int?    GetParentId(this ISession s)              => s.GetInt32("ParentId");
    public static void    SetParentPhone(this ISession s, string v) => s.SetString("ParentPhone", v);
    public static string? GetParentPhone(this ISession s)           => s.GetString("ParentPhone");
    public static void    SetParentName(this ISession s, string? v) => s.SetString("ParentName", v ?? "");
    public static string? GetParentName(this ISession s)            => s.GetString("ParentName");
}

// ── Require Parent Login ──────────────────────────────────────────────────────
public class RequireParentLoginAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (ctx.HttpContext.Session.GetInt32("ParentId") == null)
        {
            ctx.Result = new RedirectToActionResult("Login", "Parent", null);
        }
    }
}

public class RequireAdminOrParentAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        var userId = ctx.HttpContext.Session.GetInt32("UserId");
        var parentId = ctx.HttpContext.Session.GetInt32("ParentId");

        if (userId == null && parentId == null)
        {
            ctx.Result = new RedirectToActionResult("Login", "Parent", null);
        }
    }
}

// ── Finance Dashboard — Only for user ID 10 (Sunil) ──────────────────────────────
public class RequireFinanceAdminAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        var userId = ctx.HttpContext.Session.GetInt32("UserId");

        if (userId != 10)
        {
            ctx.Result = new RedirectToActionResult("Index", "Dashboard",
                new { msg = "access_denied" });
        }
    }
}
