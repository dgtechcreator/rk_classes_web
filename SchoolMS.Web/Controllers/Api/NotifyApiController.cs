using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>Send Notification (admin only): message to everybody / a group / chosen students or staff.</summary>
[ApiController]
[Route("api/notify")]
[ApiRequireAdmin]
public class NotifyApiController(BroadcastService svc) : ControllerBase
{
    int Uid => User.UserId() ?? 0;

    [HttpPost("send")]
    public IActionResult Send([FromBody] BroadcastReq req)
    {
        var (error, students, staff) = svc.Send(req, Uid);
        return error != null
            ? BadRequest(new { success = false, error, message = error })
            : Ok(new { success = true, students, staff, message = $"Sent to {students} student{(students == 1 ? "" : "s")} and {staff} staff." });
    }

    [HttpGet("staff")]
    public IActionResult Staff() => Ok(new { staff = svc.StaffList(Uid).Select(s => new { id = s.id, name = s.name, role = s.role }) });

    [HttpGet("students")]
    public IActionResult Students(string? q) => Ok(new { students = svc.FindStudents(q).Select(s => new { id = s.id, label = s.label }) });
}
