using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

// Admin-only (RoleId==1) — matches DeletedController.Index's real check exactly, and deliberately
// tightens the Restore* actions too: the MVC versions carry only [RequireLogin] with no further check
// (they're reachable in practice only from the admin-only Index page's buttons), but a JSON endpoint
// is directly callable by any authenticated staff member, so leaving them at bare login would let a
// non-admin restore soft-deleted student/faculty/receipt records via a raw HTTP call — an elevation
// this admin-only Index was clearly never meant to allow.
[ApiController]
[Route("api/deleted")]
[ApiRequireAdmin]
public class DeletedApiController(StudentService studentSvc, FacultyService facultySvc, FeesService feesSvc, UserMgmtService userSvc, UserAccessCache access, DeletedRecordsService othersSvc) : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        var (students, _) = studentSvc.GetAll(1, 10000, null, null, null, null, null, "Inactive");
        var (faculty, _) = facultySvc.GetAll(null, "Inactive", null, 1, 10000);
        var receipts = feesSvc.GetDeletedPayments();
        othersSvc.Enrich(students, faculty, studentSvc, facultySvc);
        var users = userSvc.GetDeleted().Select(u => new { u.UserId, u.FullName, u.Username, u.RoleId, u.RoleName, u.Email, u.Phone, u.DeletedAt, u.DeletedByName });
        return Ok(new { students, faculty, receipts, users, others = othersSvc.GetOthers() });
    }

    [HttpPost("students/{id:int}/restore")]
    public IActionResult RestoreStudent(int id)
    {
        studentSvc.Restore(id);
        return Ok(new { success = true });
    }

    [HttpPost("faculty/{id:int}/restore")]
    public IActionResult RestoreFaculty(int id)
    {
        facultySvc.Restore(id);
        return Ok(new { success = true });
    }

    [HttpPost("users/{id:int}/restore")]
    public IActionResult RestoreUser(int id)
    {
        userSvc.Restore(id);
        access.Invalidate(id);
        return Ok(new { success = true });
    }

    /// <summary>kind = expense | teacherpayment | teacherattendance | lecture</summary>
    [HttpPost("others/{kind}/{id:int}/restore")]
    public IActionResult RestoreOther(string kind, int id)
    {
        if (!othersSvc.Restore(kind, id)) return BadRequest(new { error = "Unknown record type." });
        return Ok(new { success = true });
    }

    [HttpPost("receipts/{id:int}/restore")]
    public IActionResult RestoreReceipt(int id)
    {
        feesSvc.RestorePayment(id);
        return Ok(new { success = true });
    }
}
