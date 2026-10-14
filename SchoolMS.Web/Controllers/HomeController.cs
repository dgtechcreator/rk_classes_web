using Microsoft.AspNetCore.Mvc;

namespace SchoolMS.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index","Dashboard");
    public IActionResult Error() => View();

    // Public pages (no login) - the Play Store listing links to them.
    public IActionResult PrivacyPolicy() => View();
    public IActionResult DeleteAccount() => View();
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access Denied";
        return View();
    }
}
