using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>
/// Classrooms: the master list (any staff reads it for the lecture form; only the admin / schedule editors change it),
/// the live board (which room is free or engaged now, until when, which lecture) and the room history.
/// </summary>
[ApiController]
[Route("api/classrooms")]
[ApiRequireStaff]
public class ClassroomsApiController(ClassroomService svc) : ControllerBase
{
    public record SaveReq(int ClassroomId, string Name, int? Capacity, string? Location, bool IsActive = true);

    int Uid => User.UserId() ?? 0;

    [HttpGet]
    public IActionResult List(bool all = false)
        => Ok(new { classrooms = svc.GetAll(all && User.HasPerm("lecture_schedule")).Select(c => new { id = c.ClassroomId, name = c.Name.Trim(), capacity = c.Capacity, location = c.Location, isActive = c.IsActive }) });

    [HttpPost("save")]
    [ApiRequirePermission("lecture_schedule", requireEdit: true)]
    public IActionResult Save([FromBody] SaveReq req)
    {
        var (error, id) = svc.Save(new Classroom { ClassroomId = req.ClassroomId, Name = req.Name, Capacity = req.Capacity, Location = req.Location, IsActive = req.IsActive }, Uid);
        return error != null ? BadRequest(new { success = false, error, message = error }) : Ok(new { success = true, id });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("lecture_schedule", requireEdit: true)]
    public IActionResult Delete(int id)
    {
        var error = svc.Delete(id, Uid);
        return error != null ? BadRequest(new { success = false, error, message = error }) : Ok(new { success = true });
    }

    /// <summary>Live board. date = a day to look at (default now); only today shows "engaged / free now".</summary>
    [HttpGet("board")]
    [ApiRequirePermission("lecture_schedule")]
    public IActionResult Board(DateTime? date) => Ok(ClassroomHelpers.BoardPayload(svc, date));

    [HttpGet("history")]
    [ApiRequirePermission("lecture_schedule")]
    public IActionResult History(int? classroomId, DateTime? from, DateTime? to, string? q)
        => Ok(ClassroomHelpers.HistoryPayload(svc, classroomId, from, to, q));
}
