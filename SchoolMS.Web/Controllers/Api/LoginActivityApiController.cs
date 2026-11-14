using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Sign-in history for the app. Everyone sees their own (mine); the admin also sees everybody's (all).</summary>
[ApiController]
[Route("api/login-activity")]
[ApiRequireStaffOrParent]
public class LoginActivityApiController(LoginActivityService svc) : ControllerBase
{
    string MyType => User.UserType() == "parent" ? "parent" : "staff";
    bool IsAdmin => MyType == "staff" && User.RoleId() == 1;

    static object Row(LoginActivityEntry e) => new {
        loginId = e.LoginId, accountType = e.AccountType, accountId = e.AccountId, loginName = e.LoginName, displayName = e.DisplayName,
        roleName = e.RoleName, success = e.Success, platform = e.Platform, device = e.Device, appVersion = e.AppVersion, ip = e.IpAddress, loggedAt = e.LoggedAt,
    };

    static object Dev(DeviceUse d) => new { device = d.Device, platform = d.Platform, logins = d.Logins, lastUsed = d.LastUsed, lastIp = d.LastIp };

    [HttpGet("mine")]
    public IActionResult Mine(int take = 100)
    {
        int id = User.UserId() ?? 0;
        return Ok(new {
            entries = svc.Mine(MyType, id, Math.Clamp(take, 1, 300)).Select(Row),
            devices = svc.Devices(MyType, id).Select(Dev),
        });
    }

    /// <param name="type">staff | parent | (empty = both)</param>
    /// <param name="result">ok | failed | (empty = both)</param>
    /// <param name="account">"staff:3" / "parent:12" - one person; the answer then also lists their devices</param>
    [HttpGet("all")]
    public IActionResult All(string? type, string? q, string? from, string? to, string? result, string? account, int take = 300)
    {
        if (!IsAdmin) return StatusCode(403, new { error = "Only the admin can see everybody's login activity." });

        string? t = type is "staff" or "parent" ? type : null;
        bool? ok = result == "ok" ? true : result == "failed" ? false : null;
        DateTime? f = DateTime.TryParse(from, out var fd) ? fd.Date : null;
        DateTime? tt = DateTime.TryParse(to, out var td) ? td.Date : null;

        string? accType = null; int? accId = null;
        if (!string.IsNullOrWhiteSpace(account))
        {
            var parts = account.Split(':');
            if (parts.Length == 2 && (parts[0] == "staff" || parts[0] == "parent") && int.TryParse(parts[1], out var id)) { accType = parts[0]; accId = id; }
        }

        var entries = svc.Query(accType ?? t, accId, q, f, tt, ok, Math.Clamp(take, 1, 1000));
        return Ok(new {
            entries = entries.Select(Row),
            devices = accType != null && accId != null ? svc.Devices(accType, accId.Value).Select(Dev) : Enumerable.Empty<object>(),
        });
    }
}
