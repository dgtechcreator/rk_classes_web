using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// Class Representatives (CR / Monitor) - admin only. Make a student of a class the CR, give him a login, switch him
/// on / off. A CR logs in like any user but sees and saves only his own class: student attendance and teacher
/// attendance (subject + topic). Enforced server-side for web and the mobile API.
/// </summary>
[RequireLogin]
public class ClassRepController(ClassRepService svc, StudentService students, LookupService lookup, UserAccessCache access) : Controller
{
    public record AssignReq(int StudentId, string Username, string Password, bool LimitToBatch, string[]? Rights = null);
    public record UpdateReq(int CrId, bool LimitToBatch, bool IsActive, string? NewPassword, string[]? Rights = null);

    bool IsAdmin => HttpContext.Session.GetInt32("RoleId") == 1;
    int Me => HttpContext.Session.GetUserId() ?? 0;

    public IActionResult Index()
    {
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Home");
        try
        {
            ViewBag.Reps = svc.GetAll();
            ViewBag.Ready = true;
        }
        catch
        {
            ViewBag.Reps = new List<ClassRep>();
            ViewBag.Ready = false;   // SQL/61 not run yet
        }
        ViewBag.Classes = lookup.GetClasses();
        ViewBag.Sections = lookup.GetSections();
        return View();
    }

    /// <summary>Active students of a class (optionally one section) for the picker, excluding students who already are CR.</summary>
    [HttpGet]
    public IActionResult Students(int classId, int? sectionId)
    {
        if (!IsAdmin) return Unauthorized();
        var taken = new HashSet<int>();
        try { taken = svc.GetAll().Select(c => c.StudentId).ToHashSet(); } catch { }
        var (list, _) = students.GetAll(1, 5000, null, classId, sectionId, null, null, "Active");
        return Json(list.Where(s => !taken.Contains(s.StudentId)).OrderBy(s => s.DisplayName).Select(s => new {
            studentId = s.StudentId, name = s.DisplayName, admissionNo = s.AdmissionNo,
            className = s.ClassName, sectionName = s.SectionName, batchName = s.BatchName, hasBatch = s.BatchId != null,
        }));
    }

    [HttpPost]
    public IActionResult Assign([FromBody] AssignReq req)
    {
        if (!IsAdmin) return Unauthorized();
        try
        {
            var (ok, message, crId) = svc.Assign(req.StudentId, req.Username, req.Password, req.LimitToBatch, Me, req.Rights);
            return Json(new { success = ok, message, crId });
        }
        catch (Exception ex) { return Json(new { success = false, message = "Could not save: " + ex.Message }); }
    }

    [HttpPost]
    public IActionResult Update([FromBody] UpdateReq req)
    {
        if (!IsAdmin) return Unauthorized();
        try
        {
            var (ok, message) = svc.Update(req.CrId, req.LimitToBatch, req.IsActive, req.NewPassword, Me, req.Rights);
            var cr = svc.GetById(req.CrId);
            if (cr != null) access.Invalidate(cr.UserId);
            return Json(new { success = ok, message });
        }
        catch (Exception ex) { return Json(new { success = false, message = "Could not save: " + ex.Message }); }
    }
}
