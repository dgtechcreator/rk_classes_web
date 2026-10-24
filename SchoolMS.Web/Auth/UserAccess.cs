using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolMS.DB;
using SchoolMS.Domain;
using SchoolMS.Repository;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Auth;

/// <summary>
/// Live view of what one staff login may do RIGHT NOW. Permissions used to be frozen into the web session / JWT at
/// login, so a changed right, a deactivated or deleted user only took effect after the next login. Every web
/// request and every API token is now checked against this (cached a few seconds, cleared the moment an admin
/// saves rights), so changes apply immediately.
/// </summary>
public class UserAccess
{
    /// <summary>False when the DB could not be read - callers then keep what the session / token already says.</summary>
    public bool Known { get; init; } = true;
    public bool Active { get; init; }
    public int RoleId { get; init; }
    public string RoleName { get; init; } = "";
    public string? ProfilePicPath { get; init; }
    public Dictionary<string, bool> Perms { get; init; } = new();
    public CrScope? Cr { get; init; }

    public bool IsClassRepRole => string.Equals(RoleName, "Class Representative", StringComparison.OrdinalIgnoreCase);

    /// <summary>A Class Representative login whose CR access is off (revoked, student left / inactive).</summary>
    public bool CrBlocked => IsClassRepRole && Cr == null;

    /// <summary>Login may continue only when the user exists, is active and (for CR logins) the CR access is working.</summary>
    public bool CanUse => Active && !CrBlocked;
}

/// <summary>Singleton cache of <see cref="UserAccess"/> per user id.</summary>
public class UserAccessCache(CommonConnectivity db)
{
    static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);
    readonly ConcurrentDictionary<int, (DateTime at, UserAccess? acc)> _cache = new();

    public UserAccess? Get(int userId)
    {
        if (_cache.TryGetValue(userId, out var hit) && DateTime.UtcNow - hit.at < Ttl) return hit.acc;
        UserAccess? acc;
        try { acc = Load(userId); }
        catch
        {
            // DB hiccup: serve the last known answer, or "unknown" (callers fall back to session / token).
            return _cache.TryGetValue(userId, out var stale) ? stale.acc : new UserAccess { Known = false, Active = true };
        }
        _cache[userId] = (DateTime.UtcNow, acc);
        return acc;
    }

    public void Invalidate(int userId) => _cache.TryRemove(userId, out _);
    public void InvalidateAll() => _cache.Clear();

    UserAccess? Load(int userId)
    {
        // u.* (not named columns) so a database without Users.ProfilePicPath yet still loads - G<> returns null for a missing column.
        var rows = db.SqlP("SELECT u.*, r.RoleName FROM Users u LEFT JOIN Roles r ON r.RoleId=u.RoleId WHERE u.UserId=@Id",
            new() { { "@Id", userId } },
            r => new { Active = CommonConnectivity.G<bool>(r, "IsActive"), RoleId = CommonConnectivity.G<int>(r, "RoleId"), RoleName = CommonConnectivity.G<string>(r, "RoleName") ?? "", Pic = CommonConnectivity.G<string>(r, "ProfilePicPath") });
        var u = rows.FirstOrDefault();
        if (u == null) return null;   // user no longer exists

        CrScope? cr = null;
        try
        {
            var rep = new ClassRepRepo(db).GetByUserId(userId);
            if (rep != null && rep.IsWorking) cr = ClassRepService.ToScope(rep);
        }
        catch { /* ClassRepresentatives table not created yet (SQL/61 not run) -> nobody is a CR */ }

        // A teacher / admin login has no foreign key to its Faculty profile (the system matches them by phone, then name -
        // see TeacherAccountLinker). When the login has no photo of its own, show the linked Faculty profile's photo, so one
        // photo set on the Faculty page is also the teacher's photo in the app and the website.
        var pic = u.Pic;
        if (string.IsNullOrEmpty(pic) && StaffNameService.IsTeacherOrAdmin(u.RoleId, u.RoleName))
        {
            try
            {
                var linker = new TeacherAccountLinker(new UserMgmtService(new UserMgmtRepo(db)), new FacultyService(new FacultyRepo(db)));
                pic = linker.FindFaculty(userId)?.ProfilePicPath;
            }
            catch { /* no fallback photo - initials are shown */ }
        }

        return new UserAccess {
            Active = u.Active, RoleId = u.RoleId, RoleName = u.RoleName, ProfilePicPath = pic,
            Perms = new UserMgmtRepo(db).GetPermissions(userId), Cr = cr,
        };
    }
}

public static class UserAccessExtensions
{
    /// <summary>
    /// Ids of the active students a CR may work with. Attendance rows from sp_GetAttendance carry class / section / batch
    /// NAMES but not their ids, so scope is resolved from the student list (which does have the ids) and rows are matched by student id.
    /// </summary>
    public static HashSet<int> StudentIds(this CrScope cr, StudentService students)
    {
        var (list, _) = students.GetAll(1, 10000, null, cr.ClassId, cr.SectionId, cr.EffectiveBatchId, null, "Active");
        return list.Where(cr.Matches).Select(s => s.StudentId).ToHashSet();
    }

    /// <summary>The Class Representative scope of the logged-in staff user (web session or API token); null for everyone else.</summary>
    public static CrScope? CurrentCr(this HttpContext http)
    {
        var uid = http.CurrentStaffUserId();
        if (uid == null) return null;
        return http.RequestServices.GetService<UserAccessCache>()?.Get(uid.Value)?.Cr;
    }

    public static int? CurrentStaffUserId(this HttpContext http)
    {
        if (http.Request.Path.StartsWithSegments("/api"))
            return http.User.UserType() == "staff" ? http.User.UserId() : null;
        return http.Session.GetInt32("UserId");
    }

    public static bool SamePerms(Dictionary<string, bool> a, Dictionary<string, bool> b)
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    /// <summary>Keeps the web session in step with the DB (rights, role) - or signs the user out when disabled.</summary>
    public static void RefreshSession(this HttpContext http, UserAccessCache cache)
    {
        var uid = http.Session.GetInt32("UserId");
        if (uid == null) return;
        var acc = cache.Get(uid.Value);
        if (acc == null || !acc.CanUse) { http.Session.Clear(); return; }
        if (!acc.Known) return;

        var s = http.Session;
        if (s.GetInt32("RoleId") != acc.RoleId) s.SetInt32("RoleId", acc.RoleId);
        if (s.GetString("RoleName") != acc.RoleName) s.SetString("RoleName", acc.RoleName);
        if (!SamePerms(s.GetPermissions(), acc.Perms)) s.SetPermissions(acc.Perms);
        if (s.GetString("DisplayName") == null)
        {
            try { s.SetString("DisplayName", http.RequestServices.GetRequiredService<StaffNameService>().DisplayName(uid.Value)); }
            catch { s.SetString("DisplayName", s.GetString("FullName") ?? ""); }
        }
        var isCr = acc.Cr != null ? 1 : 0;
        if (s.GetInt32("IsCr") != isCr) s.SetInt32("IsCr", isCr);
        var pic = acc.ProfilePicPath ?? "";
        if ((s.GetString("ProfilePic") ?? "") != pic) s.SetString("ProfilePic", pic);
    }

    /// <summary>Swaps the permission / role claims baked into a staff JWT for the live ones (or rejects a disabled user).</summary>
    public static void RefreshToken(this Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext ctx)
    {
        var p = ctx.Principal;
        if (p == null || p.UserType() != "staff") return;
        var cache = ctx.HttpContext.RequestServices.GetRequiredService<UserAccessCache>();
        var uid = p.UserId();
        var acc = uid == null ? null : cache.Get(uid.Value);
        if (acc == null || !acc.CanUse) { ctx.Fail("Account disabled."); return; }
        if (!acc.Known || p.Identity is not ClaimsIdentity id) return;

        void Replace(string type, string value)
        {
            foreach (var c in id.FindAll(type).ToList()) id.RemoveClaim(c);
            id.AddClaim(new Claim(type, value));
        }
        Replace("roleId", acc.RoleId.ToString());
        Replace("roleName", acc.RoleName);
        Replace("perms", JsonSerializer.Serialize(acc.Perms));
    }
}

/// <summary>
/// Hard fence around a Class Representative login. A CR may only use the pages / endpoints below; every other
/// controller action (web or API) is refused no matter which module permissions exist. The allowed actions then
/// enforce the CR's class scope themselves.
/// </summary>
public class ClassRepGuardFilter(UserAccessCache cache) : IActionFilter
{
    static readonly Dictionary<string, string[]> Web = new(StringComparer.OrdinalIgnoreCase) {
        ["Account"]           = new[] { "*" },
        ["Home"]              = new[] { "*" },
        ["Attendance"]        = new[] { "Index", "Save", "GetDateWiseAttendance", "GetStudentContact" },
        ["TeacherAttendance"] = new[] { "Index", "SaveAttendance", "DeleteAttendance", "Summary" },
        ["Marks"]             = new[] { "Index", "Save" },
        ["Users"]             = new[] { "Edit", "Save", "ChangePassword" },
    };

    static readonly Dictionary<string, string[]> Api = new(StringComparer.OrdinalIgnoreCase) {
        ["AuthApi"]              = new[] { "*" },
        ["AppConfigApi"]         = new[] { "*" },
        ["AttendanceApi"]        = new[] { "Index", "Batches", "BatchDetail", "Options", "Save", "GetDateWiseAttendance", "GetStudentContact" },
        ["TeacherAttendanceApi"] = new[] { "*" },
        ["MarksApi"]             = new[] { "Index", "Save" },
        ["LookupApi"]            = new[] { "GetYears", "GetClasses", "GetSections", "GetBatches", "GetSubjects", "GetExams" },
        ["UsersApi"]             = new[] { "GetById", "Save", "ChangePassword", "UploadPhoto", "DeletePhoto" },
    };

    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        var http = ctx.HttpContext;
        bool isApi = http.Request.Path.StartsWithSegments("/api");
        var uid = http.CurrentStaffUserId();
        if (uid == null) return;
        var acc = cache.Get(uid.Value);
        if (acc?.Cr == null) return;   // not a Class Representative

        if (ctx.ActionDescriptor is ControllerActionDescriptor cd)
        {
            var map = isApi ? Api : Web;
            if (map.TryGetValue(cd.ControllerName, out var actions)
                && (actions.Contains("*") || actions.Contains(cd.ActionName, StringComparer.OrdinalIgnoreCase)))
                return;
        }

        bool ajax = isApi || !HttpMethods.IsGet(http.Request.Method)
                    || http.Request.Headers.XRequestedWith == "XMLHttpRequest"
                    || http.Request.Headers.Accept.ToString().Contains("json", StringComparison.OrdinalIgnoreCase);
        if (ajax)
            ctx.Result = new ObjectResult(new { success = false, error = "Access denied.", message = "Access denied." }) { StatusCode = 403 };
        else
            ctx.Result = new RedirectToActionResult("Index", "Attendance", null);
    }

    public void OnActionExecuted(ActionExecutedContext ctx) { }
}
