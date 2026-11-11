using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolMS.Services;

namespace SchoolMS.Web.Auth;

/// <summary>
/// Puts an action behind one section of the Student / Parent portal (Users &amp; Permissions -> Roles &amp; Access).
/// When the admin has switched that section off the request is refused: the API answers 403 with a message, a
/// web page goes back to the portal home. Staff logins never reach these actions, so nothing else changes for them.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PortalSectionAttribute(string section) : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext ctx)
    {
        var svc = ctx.HttpContext.RequestServices.GetRequiredService<PortalAccessService>();
        if (svc.IsVisible(section)) return;

        var http = ctx.HttpContext;
        bool json = http.Request.Path.StartsWithSegments("/api")
                    || http.Request.Headers.XRequestedWith == "XMLHttpRequest"
                    || http.Request.Headers.Accept.ToString().Contains("json", StringComparison.OrdinalIgnoreCase);
        if (json)
            ctx.Result = new ObjectResult(new { error = "This section is not available for your login.", message = "This section is not available for your login." }) { StatusCode = 403 };
        else
            ctx.Result = new RedirectToActionResult("Dashboard", "Parent", null);
    }
}
