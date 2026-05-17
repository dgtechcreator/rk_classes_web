using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class DashboardController(LookupService lookup) : Controller
{
    public IActionResult Index() => View(lookup.GetDashStats());
}
