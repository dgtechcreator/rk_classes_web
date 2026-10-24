using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>
/// Public (no login) settings the mobile app asks for on start / resume, before and after login, so a website-side
/// switch such as maintenance mode reaches every phone. Only things that are safe for anyone to see.
/// </summary>
[ApiController]
[Route("api/app-config")]
public class AppConfigApiController(MaintenanceService maintenance) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var m = maintenance.Get();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { maintenanceEnabled = m.Enabled, maintenanceMessage = m.Message });
    }
}
