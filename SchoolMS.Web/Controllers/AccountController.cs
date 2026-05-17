using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.ViewModels;

namespace SchoolMS.Web.Controllers;

public class AccountController(AuthService auth, UserMgmtService userMgmt) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (HttpContext.Session.GetUserId() != null) return RedirectToAction("Index","Dashboard");
        return View(new LoginVM());
    }
    [HttpPost]
    public IActionResult Login(LoginVM m, string? returnUrl)
    {
        var (ok, u, msg) = auth.Login(m.Username, m.Password);
        if (!ok) { m.Error = msg; return View(m); }
        HttpContext.Session.SetUser(u!);
        var perms = userMgmt.GetPermissions(u!.UserId);
        HttpContext.Session.SetPermissions(perms);
        return Redirect(returnUrl ?? "/Dashboard/Index");
    }
    public IActionResult Logout() { HttpContext.Session.Clear(); return RedirectToAction("Login"); }
}
