using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Fee follow-up for the app: summary, one student's history, add / delete a follow-up, and the Excel export. Right: fees_due.</summary>
[ApiController]
[Route("api/fee-followups")]
[ApiRequirePermission("fees_due")]
public class FeeFollowupsApiController(FeeFollowupService svc, UserMgmtService users) : ControllerBase
{
    public record AddReq(int StudentId, string Mode, string? SpokeTo, string Outcome, string? Reason, string? Notes, decimal? PromisedAmount,
        string? NextDate, string? NextDateBy, string? FollowupAt);

    int Uid => User.UserId() ?? 0;

    static FeeFollowupService.SummaryFilter Filter(string? q, string? status, string? className, string? sectionName, string? batchName, int? followedBy, string? outcome, string? from, string? to)
        => new() {
            Q = q, Status = status, ClassName = className, SectionName = sectionName, BatchName = batchName, FollowedBy = followedBy, Outcome = outcome,
            From = DateTime.TryParse(from, out var f) ? f.Date : null, To = DateTime.TryParse(to, out var t) ? t.Date : null,
        };

    [HttpGet("options")]
    public IActionResult Options() => Ok(new { modes = FeeFollowupRules.Modes, spokeTo = FeeFollowupRules.SpokeTo, outcomes = FeeFollowupRules.Outcomes });

    [HttpGet("summary")]
    public IActionResult Summary(string? q, string? status, string? className, string? sectionName, string? batchName, int? followedBy, string? outcome, string? from, string? to)
        => Ok(FollowupHelpers.SummaryPayload(svc.Summary(Filter(q, status, className, sectionName, batchName, followedBy, outcome, from, to))));

    [HttpGet("student/{studentId:int}")]
    public IActionResult Student(int studentId)
    {
        var fin = svc.Finance(studentId);
        var rows = svc.History(studentId);
        var name = fin?.FullName ?? rows.FirstOrDefault()?.StudentName ?? "";
        return Ok(new {
            student = new {
                studentId, name, admissionNo = fin?.AdmissionNo ?? rows.FirstOrDefault()?.AdmissionNo, className = fin?.ClassName, sectionName = fin?.SectionName, batchName = fin?.BatchName,
                phone = fin?.Phone, fatherPhone = fin?.FatherPhone, motherPhone = fin?.MotherPhone, balance = fin?.Balance ?? 0, totalFees = fin?.TotalFees ?? 0, collected = fin?.Collected ?? 0,
            },
            followups = rows.Select(r => new { entry = FollowupHelpers.Entry(r), canDelete = User.RoleId() == 1 || r.FollowedBy == Uid }),
        });
    }

    [HttpPost("add")]
    public IActionResult Add([FromBody] AddReq req)
    {
        var f = new FeeFollowup {
            StudentId = req.StudentId, Mode = req.Mode, SpokeTo = string.IsNullOrWhiteSpace(req.SpokeTo) ? null : req.SpokeTo, Outcome = req.Outcome, Reason = req.Reason, Notes = req.Notes,
            PromisedAmount = req.PromisedAmount, NextDate = DateTime.TryParse(req.NextDate, out var nd) ? nd.Date : null, NextDateBy = req.NextDateBy,
            FollowupAt = DateTime.TryParse(req.FollowupAt, out var at) ? at : DateTime.Now,
        };
        var (error, id) = svc.Add(f, Uid);
        return error != null ? BadRequest(new { success = false, error, message = error }) : Ok(new { success = true, id });
    }

    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        var error = svc.Delete(id, Uid, User.RoleId() == 1);
        return error != null ? BadRequest(new { success = false, error, message = error }) : Ok(new { success = true });
    }

    /// <summary>Excel (CSV): summary + every follow-up in detail. Opened by the app with ?token= (a link outside the app).</summary>
    [HttpGet("export")]
    public IActionResult Export(string? q, string? status, string? className, string? sectionName, string? batchName, int? followedBy, string? outcome, string? from, string? to)
    {
        var flt = Filter(q, status, className, sectionName, batchName, followedBy, outcome, from, to);
        var summary = svc.Summary(flt);
        var ids = summary.Select(s => s.StudentId).ToHashSet();
        var details = svc.Details(flt.From, flt.To, followedBy, outcome).Where(d => ids.Contains(d.StudentId)).ToList();
        return File(FollowupHelpers.Csv(summary, details), "text/csv", $"FeeFollowups_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }
}
