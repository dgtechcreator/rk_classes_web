using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Class Representative management for the mobile app - admin only, same rules as ClassRepController.</summary>
[ApiController]
[Route("api/class-reps")]
[ApiRequireAdmin]
public class ClassRepApiController(ClassRepService svc, StudentService students, UserAccessCache access) : ControllerBase
{
    public record AssignReq(int StudentId, string Username, string Password, bool LimitToBatch, string[]? Rights = null);
    public record UpdateReq(bool LimitToBatch, bool IsActive, string? NewPassword, string[]? Rights = null);

    [HttpGet]
    public IActionResult Index()
    {
        try { return Ok(svc.GetAll()); }
        catch { return StatusCode(500, new { error = "Class Representative tables are missing. Run SQL/61_ClassRepresentative.sql first." }); }
    }

    /// <summary>Active students of a class (optionally section) who are not CR yet.</summary>
    [HttpGet("students")]
    public IActionResult Students(int classId, int? sectionId)
    {
        var taken = new HashSet<int>();
        try { taken = svc.GetAll().Select(c => c.StudentId).ToHashSet(); } catch { }
        var (list, _) = students.GetAll(1, 5000, null, classId, sectionId, null, null, "Active");
        return Ok(list.Where(s => !taken.Contains(s.StudentId)).OrderBy(s => s.DisplayName).Select(s => new {
            studentId = s.StudentId, name = s.DisplayName, admissionNo = s.AdmissionNo,
            className = s.ClassName, sectionName = s.SectionName, batchName = s.BatchName, hasBatch = s.BatchId != null,
        }));
    }

    [HttpPost]
    public IActionResult Assign([FromBody] AssignReq req)
    {
        try
        {
            var (ok, message, crId) = svc.Assign(req.StudentId, req.Username, req.Password, req.LimitToBatch, User.UserId() ?? 0, req.Rights);
            return Ok(new { success = ok, message, crId });
        }
        catch (Exception ex) { return Ok(new { success = false, message = "Could not save: " + ex.Message }); }
    }

    [HttpPost("{crId:int}/update")]
    public IActionResult Update(int crId, [FromBody] UpdateReq req)
    {
        try
        {
            var (ok, message) = svc.Update(crId, req.LimitToBatch, req.IsActive, req.NewPassword, User.UserId() ?? 0, req.Rights);
            var cr = svc.GetById(crId);
            if (cr != null) access.Invalidate(cr.UserId);
            return Ok(new { success = ok, message });
        }
        catch (Exception ex) { return Ok(new { success = false, message = "Could not save: " + ex.Message }); }
    }
}
