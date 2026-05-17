using Microsoft.AspNetCore.Mvc;

namespace SchoolMS.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index","Dashboard");
    public IActionResult Error() => View();
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access Denied";
        return View();
    }
}
