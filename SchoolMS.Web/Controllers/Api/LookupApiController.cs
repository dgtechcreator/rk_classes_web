using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/lookup")]
[ApiRequireStaff]
public class LookupApiController(LookupService lookup) : ControllerBase
{
    [HttpGet("years")]
    public IActionResult GetYears() => Ok(lookup.GetYears());

    [HttpGet("classes")]
    public IActionResult GetClasses() => Ok(lookup.GetClasses());

    [HttpGet("sections")]
    public IActionResult GetSections() => Ok(lookup.GetSections());

    [HttpGet("batches")]
    public IActionResult GetBatches() => Ok(lookup.GetBatches());

    [HttpGet("fee-types")]
    public IActionResult GetFeeTypes() => Ok(lookup.GetFeeTypes());

    [HttpGet("exp-cats")]
    public IActionResult GetExpCats() => Ok(lookup.GetExpCats());

    [HttpGet("subjects")]
    public IActionResult GetSubjects(int classId) => Ok(lookup.GetSubjects(classId));

    [HttpGet("exams")]
    public IActionResult GetExams() => Ok(lookup.GetExams());

    [HttpGet("students-dropdown")]
    public IActionResult GetStudentDropdown() => Ok(lookup.GetStudentDropdown());
}
