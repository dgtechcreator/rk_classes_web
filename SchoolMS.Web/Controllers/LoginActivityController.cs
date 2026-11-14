using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// Sign-in history for privacy / security. The admin sees everybody's (staff, teachers, students / parents) and can open one
/// person's list of devices; every other login (staff or student / parent) sees only its own history.
/// </summary>
public class LoginActivityController(LoginActivityService svc) : Controller
{
    public class IndexVM
    {
        public List<LoginActivityEntry> Entries { get; set; } = new();
        public List<DeviceUse> Devices { get; set; } = new();
        public string? DevicesOf { get; set; }
        public string? Type { get; set; }
        public string? Q { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Result { get; set; }
    }

    // ── Admin: everybody ─────────────────────────────────────────
    [RequireLogin]
    public IActionResult Index(string? type, string? q, DateTime? from, DateTime? to, string? result, string? account)
    {
        if (HttpContext.Session.GetInt32("RoleId") != 1) return RedirectToAction("AccessDenied", "Home");

        string? t = type is "staff" or "parent" ? type : null;
        bool? ok = result == "ok" ? true : result == "failed" ? false : null;

        // account = "staff:3" / "parent:12": one person (click on a name) -> also lists the devices they used
        string? accType = null; int? accId = null;
        if (!string.IsNullOrWhiteSpace(account))
        {
            var parts = account.Split(':');
            if (parts.Length == 2 && parts[0] is "staff" or "parent" && int.TryParse(parts[1], out var id)) { accType = parts[0]; accId = id; }
        }

        var vm = new IndexVM {
            Type = t, Q = q, From = from?.ToString("yyyy-MM-dd"), To = to?.ToString("yyyy-MM-dd"), Result = result,
            Entries = svc.Query(accType ?? t, accId, q, from, to, ok, 500),
        };
        if (accType != null && accId != null)
        {
            vm.Devices = svc.Devices(accType, accId.Value);
            vm.DevicesOf = vm.Entries.FirstOrDefault()?.DisplayName ?? vm.Entries.FirstOrDefault()?.LoginName ?? account;
        }
        return View(vm);
    }

    // ── Everybody: my own history ────────────────────────────────
    public IActionResult Mine()
    {
        string type; int id;
        var uid = HttpContext.Session.GetInt32("UserId");
        var pid = HttpContext.Session.GetInt32("ParentId");
        if (uid != null) { type = "staff"; id = uid.Value; }
        else if (pid != null) { type = "parent"; id = pid.Value; }
        else return Redirect("/Account/Login");

        ViewBag.IsParent = type == "parent";
        ViewBag.Devices = svc.Devices(type, id);
        return View(svc.Mine(type, id, 100));
    }
}
