using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>
/// Public (no login) settings the mobile app asks for on start / resume, before and after login, so a website-side
/// switch such as maintenance mode reaches every phone. Only things that are safe for anyone to see.
/// </summary>
[ApiController]
[Route("api/app-config")]
public class AppConfigApiController(MaintenanceService maintenance, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>When this website build was compiled (UTC) - open /api/app-config to see which build is live after a deploy.</summary>
    static readonly string BuildStamp = System.IO.File.GetLastWriteTimeUtc(typeof(AppConfigApiController).Assembly.Location).ToString("yyyy-MM-dd HH:mm 'UTC'");

    [HttpGet]
    public IActionResult Get()
    {
        var m = maintenance.Get();
        Response.Headers.CacheControl = "no-store";
        var u = ReadUpdateInfo();
        return Ok(new {
            maintenanceEnabled = m.Enabled, maintenanceMessage = m.Message, serverBuild = BuildStamp,
            // "A new version of the app is out": the app compares latestBuild with its own build number and offers apkUrl.
            latestBuild = u.latestBuild, latestVersion = u.latestVersion, apkUrl = u.apkUrl,
            updateMessage = u.message, forceUpdate = u.force,
        });
    }

    /// <summary>
    /// wwwroot/app/version.json, edited by hand when a new APK is released (together with wwwroot/app/RKClasses.apk):
    /// { "latestBuild": 11, "latestVersion": "1.0.3", "apkUrl": "/app/RKClasses.apk", "message": "What is new", "forceUpdate": false }.
    /// No file (or a broken one) simply means "no update to offer".
    /// </summary>
    (int latestBuild, string? latestVersion, string? apkUrl, string? message, bool force) ReadUpdateInfo()
    {
        try
        {
            var file = Path.Combine(env.WebRootPath ?? "", "app", "version.json");
            if (!System.IO.File.Exists(file)) return (0, null, null, null, false);
            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(file));
            var r = doc.RootElement;
            string? S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
            int build = r.TryGetProperty("latestBuild", out var b) && b.TryGetInt32(out var n) ? n : 0;
            bool force = r.TryGetProperty("forceUpdate", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.True;
            return (build, S("latestVersion"), S("apkUrl") ?? "/app/RKClasses.apk", S("message"), force);
        }
        catch { return (0, null, null, null, false); }
    }
}
