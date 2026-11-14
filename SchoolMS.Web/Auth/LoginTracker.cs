using System.Text.RegularExpressions;
using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Auth;

/// <summary>
/// Writes one LoginActivity row per sign-in attempt (staff, student / parent; website and app). It never throws: a logging
/// problem must not stop anybody from signing in.
/// The app sends who it is in headers (X-Platform, X-Device, X-App-Version); a browser is described from its User-Agent.
/// </summary>
public class LoginTracker(LoginActivityService svc)
{
    public void Record(HttpContext http, string accountType, int? accountId, string? loginName, string? displayName, string? roleName, bool success)
    {
        try
        {
            var h = http.Request.Headers;
            string? appDevice = h["X-Device"].FirstOrDefault();
            string platform = !string.IsNullOrWhiteSpace(appDevice) || string.Equals(h["X-Platform"].FirstOrDefault(), "app", StringComparison.OrdinalIgnoreCase) ? "app" : "web";
            string device = !string.IsNullOrWhiteSpace(appDevice) ? appDevice!
                          : platform == "app" ? "Mobile app" : DescribeBrowser(h.UserAgent.ToString());

            svc.Add(new LoginActivityEntry {
                AccountType = accountType, AccountId = accountId, LoginName = loginName ?? "", DisplayName = displayName, RoleName = roleName,
                Success = success, Platform = platform, Device = device, AppVersion = h["X-App-Version"].FirstOrDefault(),
                IpAddress = ClientIp(http),
            });
        }
        catch { /* never block a sign-in */ }
    }

    // Behind IIS / a proxy the real address is in X-Forwarded-For.
    static string? ClientIp(HttpContext http)
    {
        var fwd = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd)) return fwd.Split(',')[0].Trim();
        return http.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>"Chrome 126 · Windows 10", "Safari · iPhone iOS 17", "Chrome 126 · Samsung SM-A515F Android 13" ...</summary>
    public static string DescribeBrowser(string ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return "Unknown browser";

        string browser =
            ua.Contains("Edg/")      ? "Edge" + Ver(ua, @"Edg/(\d+)") :
            ua.Contains("OPR/")      ? "Opera" + Ver(ua, @"OPR/(\d+)") :
            ua.Contains("SamsungBrowser") ? "Samsung Internet" + Ver(ua, @"SamsungBrowser/(\d+)") :
            ua.Contains("Firefox/")  ? "Firefox" + Ver(ua, @"Firefox/(\d+)") :
            ua.Contains("Chrome/")   ? "Chrome" + Ver(ua, @"Chrome/(\d+)") :
            ua.Contains("Safari/")   ? "Safari" + Ver(ua, @"Version/(\d+)") : "Browser";

        string os;
        var android = Regex.Match(ua, @"Android\s([\d.]+);?\s?([^;)]*)");
        if (android.Success)
        {
            var model = android.Groups[2].Value.Trim();
            os = (string.IsNullOrWhiteSpace(model) || model.Equals("K", StringComparison.OrdinalIgnoreCase) ? "" : model + " ") + "Android " + android.Groups[1].Value.Split('.')[0];
        }
        else if (ua.Contains("iPhone"))  os = "iPhone iOS";
        else if (ua.Contains("iPad"))    os = "iPad iOS";
        else if (ua.Contains("Windows NT 10")) os = "Windows 10/11";
        else if (ua.Contains("Windows")) os = "Windows";
        else if (ua.Contains("Mac OS X")) os = "Mac";
        else if (ua.Contains("CrOS"))    os = "Chromebook";
        else if (ua.Contains("Linux"))   os = "Linux";
        else os = "Unknown device";

        return browser + " · " + os;
    }

    static string Ver(string ua, string pattern)
    {
        var m = Regex.Match(ua, pattern);
        return m.Success ? " " + m.Groups[1].Value : "";
    }
}
