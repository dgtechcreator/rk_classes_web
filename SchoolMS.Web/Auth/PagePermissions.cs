using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Auth;

/// <summary>
/// Server-side module rights for the web pages. Until now only the sidebar hid links - anybody logged in could type
/// /Fees/Index or /Student/Index and open it. Each listed action now needs the same module right the mobile API needs
/// (View = the module key, Edit = the key's Edit flag). The admin (RoleId 1) always passes. Actions not listed here
/// (shared look-ups such as QuickSearch, message templates, receipts) are left exactly as they were.
/// </summary>
public class PagePermissionFilter : IActionFilter
{
    record Rule(string Key, bool Edit = false, string? Alt = null);

    static readonly Dictionary<string, Rule> Rules = Build();

    static Dictionary<string, Rule> Build()
    {
        var m = new Dictionary<string, Rule>(StringComparer.OrdinalIgnoreCase);
        void Add(string controller, Rule rule, params string[] actions)
        {
            foreach (var a in actions) m[$"{controller}.{a}"] = rule;
        }

        Add("Dashboard",    new("dashboard_view", false, "dashboard"), "Index", "GetPresentStudents", "FeesThisMonth", "GetAbsentStudents");

        Add("Masters",      new("masters_view"), "Index", "AttendanceBatches", "GetStudentsForBatch", "EditAttendanceBatch");
        Add("Masters",      new("masters_view", true), "SaveMessageTemplate", "DeleteMessageTemplate", "SaveYear", "DeleteYear",
            "SaveClass", "DeleteClass", "SaveSection", "DeleteSection", "SaveBatch", "DeleteBatch", "SaveSubject", "DeleteSubject",
            "SaveExpenseCat", "DeleteExpenseCat", "CreateAttendanceBatch", "DeleteAttendanceBatch", "SaveAttendanceBatch");

        Add("Faculty",      new("faculty_view"), "Index", "Details");
        Add("Faculty",      new("faculty", true), "Create", "Edit", "Save", "Delete");

        Add("Student",      new("student_view"), "Index", "Details", "AdmissionForm", "ExportExcel");
        Add("Student",      new("student", true), "Create", "Edit", "Save", "Delete", "SendWelcomeMessages");
        Add("Student",      new("student_profile"), "Profile360", "ExportProfile360");

        Add("Fees",         new("fee_collection"), "Index", "Pay", "SavePay", "UpdateAdditionalCharges", "Summary", "SummaryJson");
        Add("Fees",         new("fee_collection", true), "DeletePayment", "SetPreviousDues");
        Add("Fees",         new("fee_collect"), "Collect", "SavePayment");

        Add("FeeStructure", new("fee_structure"), "Index", "Create", "Edit", "Save", "Delete");
        Add("FeeStructure", new("fees_due"), "FeesDue", "CollectFee", "GenerateMonthly");

        Add("Expenses",     new("expenses_view"), "Index");
        Add("Expenses",     new("expenses", true), "Create", "Edit", "Save", "Delete");

        Add("Marks",        new("marks_entry"), "Index", "Save");
        Add("Marks",        new("marks_report"), "Report", "GetSubjectsWithTests", "GetTestsForSubject", "GetTestMarksDetailed", "PrintResult", "DownloadReport");
        Add("Marks",        new("marks_topstudents"), "TopStudents");
        return m;
    }

    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        var http = ctx.HttpContext;
        if (http.Request.Path.StartsWithSegments("/api")) return;                // the API has its own attributes
        if (ctx.ActionDescriptor is not ControllerActionDescriptor cd) return;
        if (!Rules.TryGetValue($"{cd.ControllerName}.{cd.ActionName}", out var rule)) return;
        if (http.Session.GetInt32("UserId") == null) return;                      // [RequireLogin] handles the login redirect
        if (http.Session.GetInt32("RoleId") == 1) return;                         // admin

        bool ok = rule.Edit ? http.Session.HasEditPerm(rule.Key)
                          : http.Session.HasPerm(rule.Key) || (rule.Alt != null && http.Session.HasPerm(rule.Alt));
        if (ok) return;

        bool ajax = !HttpMethods.IsGet(http.Request.Method)
                    || http.Request.Headers.XRequestedWith == "XMLHttpRequest"
                    || http.Request.Headers.Accept.ToString().Contains("json", StringComparison.OrdinalIgnoreCase);
        if (ajax)
            ctx.Result = new ObjectResult(new { success = false, error = "Access denied.", message = "You do not have access to this." }) { StatusCode = 403 };
        else
            ctx.Result = new RedirectToActionResult("AccessDenied", "Home", null);
    }

    public void OnActionExecuted(ActionExecutedContext ctx) { }

    /// <summary>First page this user may open - where login sends him (the dashboard is admin-only, so others must not land on a refusal).</summary>
    public static string LandingPage(bool isAdmin, Dictionary<string, bool> perms)
    {
        if (isAdmin) return "/Dashboard/Index";
        bool Has(string k) => perms.TryGetValue(k, out var v) && v;
        // The dashboard page itself is admin-only, so a normal user never lands on it.
        var order = new (string key, string url)[] {
            ("attendance_entry", "/Attendance/Index"), ("student_view", "/Student/Index"), ("marks_entry", "/Marks/Index"),
            ("fee_collection", "/Fees/Index"), ("fee_collect", "/Fees/Collect"), ("fees_due", "/FeeStructure/FeesDue"),
            ("faculty_view", "/Faculty/Index"), ("expenses_view", "/Expenses/Index"), ("marks_report", "/Marks/Report"),
            ("lecture_schedule", "/Lecture/Index"), ("teacher_attendance", "/TeacherAttendance/Index"),
        };
        foreach (var (key, url) in order) if (Has(key)) return url;
        return "/Home/AccessDenied";
    }
}
