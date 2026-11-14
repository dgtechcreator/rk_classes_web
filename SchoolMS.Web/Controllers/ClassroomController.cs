using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;
using SchoolMS.Web.Utils;

namespace SchoolMS.Web.Controllers;

/// <summary>
/// Classrooms (web): the live board (free / engaged now, until when, which lecture), the room history, and the master list.
/// Rights: lecture_schedule (View = board + history, Edit = add / edit / delete rooms).
/// </summary>
[RequireLogin]
public class ClassroomController(ClassroomService svc) : Controller
{
    int Uid => HttpContext.Session.GetUserId() ?? 0;

    public IActionResult Board() { ViewData["Title"] = "Classroom Board"; return View(); }

    [HttpGet] public IActionResult BoardJson(DateTime? date) => Json(ClassroomHelpers.BoardPayload(svc, date));

    public IActionResult History()
    {
        ViewData["Title"] = "Classroom History";
        ViewBag.Rooms = svc.GetAll(true);
        return View();
    }

    [HttpGet] public IActionResult HistoryJson(int? classroomId, DateTime? from, DateTime? to, string? q)
        => Json(ClassroomHelpers.HistoryPayload(svc, classroomId, from, to, q));

    public IActionResult Manage()
    {
        ViewData["Title"] = "Classrooms";
        return View(svc.GetAll(true));
    }

    public record SaveReq(int ClassroomId, string Name, int? Capacity, string? Location, bool IsActive = true);

    [HttpPost]
    public IActionResult Save([FromBody] SaveReq req)
    {
        var (error, id) = svc.Save(new Classroom { ClassroomId = req.ClassroomId, Name = req.Name, Capacity = req.Capacity, Location = req.Location, IsActive = req.IsActive }, Uid);
        return Json(new { success = error == null, error, id });
    }

    public record IdReq(int Id);

    [HttpPost]
    public IActionResult Delete([FromBody] IdReq req)
    {
        var error = svc.Delete(req.Id, Uid);
        return Json(new { success = error == null, error });
    }
}
