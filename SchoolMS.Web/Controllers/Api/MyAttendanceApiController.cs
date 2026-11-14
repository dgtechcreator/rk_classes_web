using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>
/// A teacher's own teacher-attendance log: every entry of theirs, including the ones an admin deleted (who deleted it and when)
/// and the ones that were edited (who edited and when). Read only.
/// </summary>
[ApiController]
[Route("api/my-attendance")]
[ApiRequireStaff]
public class MyAttendanceApiController(TeacherAttendanceService attendanceSvc, TeacherAccountLinker linker) : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        var faculty = linker.FindFaculty(User.UserId() ?? 0);
        if (faculty == null)
            return Ok(new { linked = false, message = "Your login is not linked to a faculty profile yet. Please ask the admin.", entries = Array.Empty<object>() });
        return Ok(new { linked = true, facultyName = faculty.FullName, entries = attendanceSvc.GetLogForFaculty(faculty.FacultyId) });
    }
}
