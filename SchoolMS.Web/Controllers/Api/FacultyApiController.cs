using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/faculty")]
[ApiRequireStaff]
public class FacultyApiController(FacultyService svc, ProfilePhotoStore photos) : ControllerBase
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

    // Save takes JSON only; the photo has its own multipart endpoint below (POST {id}/photo).
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
        svc.Delete(id, User.UserId() ?? 0);
        return Ok(new { success = true });
    }

    /// <summary>Upload / replace the faculty member's photo (multipart field "photo"). Same file + DB column the web form uses.</summary>
    [HttpPost("{id:int}/photo")]
    [ApiRequirePermission("faculty", requireEdit: true)]
    public IActionResult UploadPhoto(int id, IFormFile? photo)
    {
        var f = svc.GetById(id);
        if (f == null) return NotFound(new { error = "Faculty member not found." });
        var (path, err) = photos.Save(photo, "faculty");
        if (err != null) return BadRequest(new { error = err });
        svc.SetPhoto(id, path);
        photos.Delete(f.ProfilePicPath);
        return Ok(new { profilePicPath = path });
    }

    [HttpDelete("{id:int}/photo")]
    [ApiRequirePermission("faculty", requireEdit: true)]
    public IActionResult DeletePhoto(int id)
    {
        var f = svc.GetById(id);
        if (f == null) return NotFound(new { error = "Faculty member not found." });
        svc.SetPhoto(id, null);
        photos.Delete(f.ProfilePicPath);
        return Ok(new { profilePicPath = (string?)null });
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
