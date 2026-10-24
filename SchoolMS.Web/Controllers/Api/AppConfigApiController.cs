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
    /// <summary>When this website build was compiled (UTC) - open /api/app-config to see which build is live after a deploy.</summary>
    static readonly string BuildStamp = System.IO.File.GetLastWriteTimeUtc(typeof(AppConfigApiController).Assembly.Location).ToString("yyyy-MM-dd HH:mm 'UTC'");

    [HttpGet]
    public IActionResult Get()
    {
        var m = maintenance.Get();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { maintenanceEnabled = m.Enabled, maintenanceMessage = m.Message, serverBuild = BuildStamp });
    }
}
