using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/faculty")]
[ApiRequireStaff]
public class FacultyApiController(FacultyService svc) : ControllerBase
{
    [HttpGet]
    [ApiRequirePermission("faculty_view")]
    public IActionResult Index(int page = 1, string? search = null, string? status = "Active", int? designationId = null)
    {
        var (data, total) = svc.GetAll(search, status, designationId, page, 15);
        return Ok(new {
            data, total, page, totalPages = Math.Max(1, (int)Math.Ceiling((double)total / 15)),
            designations = svc.GetDesignations(),
        });
    }

    // Note: profile picture upload is deferred — Save accepts JSON only, no IFormFile support yet.
    [HttpPost("save")]
    [ApiRequirePermission("faculty", requireEdit: true)]
    public IActionResult Save([FromBody] Faculty model)
    {
        int uid = User.UserId() ?? 1;
        var id = svc.Save(model, null, uid);
        return Ok(new { facultyId = id });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("faculty", requireEdit: true)]
    public IActionResult Delete(int id)
    {
        svc.Delete(id);
        return Ok(new { success = true });
    }

    [HttpGet("{id:int}")]
    [ApiRequirePermission("faculty_view")]
    public IActionResult Details(int id)
    {
        var f = svc.GetById(id);
        if (f == null) return NotFound();
        return Ok(new { faculty = f, subjects = svc.GetSubjects(id) });
    }
}
