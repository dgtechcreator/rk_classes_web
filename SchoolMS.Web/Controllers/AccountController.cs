using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

public class AccountController(AuthService auth, UserMgmtService userMgmt, UserAccessCache access, StaffNameService names) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (HttpContext.Session.GetInt32("UserId") != null)
            return Redirect(SafeReturn(returnUrl) ?? "/Dashboard/Index");

        HttpContext.Session.Clear();
        return View(new LoginVM());
    }

    [HttpPost]
    public IActionResult Login(LoginVM m, string? returnUrl)
    {
        var (ok, u, msg) = auth.Login(m.Username, m.Password);
        if (!ok) { m.Error = msg; return View(m); }

        access.Invalidate(u!.UserId);   // read the freshest rights at login
        var acc = access.Get(u.UserId);
        if (acc != null && !acc.CanUse)
        {
            m.Error = acc.CrBlocked ? "Your Class Representative access is switched off. Please contact the admin." : "This account is disabled. Please contact the admin.";
            return View(m);
        }

        HttpContext.Session.SetUser(u);
        HttpContext.Session.SetString("DisplayName", SafeDisplayName(u.UserId, u.FullName));
        var perms = acc is { Known: true } ? acc.Perms : userMgmt.GetPermissions(u.UserId);
        HttpContext.Session.SetPermissions(perms);

        // A Class Representative lands straight on the attendance page (the only place he works).
        var home = acc?.Cr != null ? "/Attendance/Index" : PagePermissionFilter.LandingPage(u.RoleId == 1, perms);
        return Redirect(SafeReturn(returnUrl) ?? home);
    }

    // "Kapil Sir" / "Pooja Ma'am" for teachers and admins; never lets a lookup problem block the login.
    string SafeDisplayName(int userId, string fallback)
    {
        try { var n = names.DisplayName(userId); return string.IsNullOrWhiteSpace(n) ? fallback : n; }
        catch { return fallback; }
    }

    // Only same-site paths are accepted as a return address (an open redirect would let a phishing link bounce off the real login page).
    string? SafeReturn(string? returnUrl) => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}
