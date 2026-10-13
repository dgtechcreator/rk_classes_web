using System.Text;
using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// Lists everything that is missing or inconsistent in student / fee data. Read-only except for the
/// "Apply fee" button, which only adds the fee heads that already exist for the student's own class + medium.
[RequirePermission("fee_structure")]
public class DataHealthController(DataHealthService svc, FeeStructureService feeStructureSvc) : Controller
{
    [HttpGet("/DataHealth")]
    public IActionResult Index() => View(svc.Build());

    [HttpPost("/DataHealth/ApplyFees/{id:int}")]
    public IActionResult ApplyFees(int id)
    {
        feeStructureSvc.ApplyToStudent(id);
        TempData["Success"] = "Fee applied from the student's own class / medium fee head.";
        return Redirect("/DataHealth");
    }

    [HttpGet("/DataHealth/Export")]
    public IActionResult Export()
    {
        var report = svc.Build();
        string Q(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("\uFEFFSeverity,Issue,Student,Admission No,Class,Medium,Batch,Detail,What to do\r\n");
        foreach (var i in report.Issues)
            sb.Append(string.Join(",", Q(i.Severity), Q(i.Title), Q(i.FullName), Q(i.AdmissionNo), Q(i.ClassName), Q(i.SectionName), Q(i.BatchName), Q(i.Detail), Q(i.Action))).Append("\r\n");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"data_health_{DateTime.Today:yyyy-MM-dd}.csv");
    }
}
