using Microsoft.AspNetCore.Mvc;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// A logged-in teacher's OWN payment history (when each month's payment was made). Only ever returns the
/// payments of the faculty profile linked to the caller — there is no id parameter to tamper with.
[ApiController]
[Route("api/teacher-payment/mine")]
[ApiRequireStaff]
public class MyTeacherPaymentsApiController(TeacherPaymentService paymentSvc, TeacherAccountLinker linker) : ControllerBase
{
    [HttpGet]
    public IActionResult Mine(int? year = null)
    {
        try
        {
            var faculty = linker.FindFaculty(User.UserId() ?? 0);
            if (faculty == null)
                return Ok(new { linked = false, message = "Your login is not linked to a faculty profile yet. Please ask the admin." });

            var all = paymentSvc.Search(null, null, faculty.FacultyId, null);
            var years = all.Select(p => p.PaymentYear).Distinct().OrderByDescending(v => v).ToList();
            var list = year.HasValue && year.Value > 0 ? all.Where(p => p.PaymentYear == year.Value).ToList() : all;

            return Ok(new {
                linked = true,
                facultyId = faculty.FacultyId,
                facultyName = faculty.FullName.Trim(),
                year = year ?? 0,
                years,
                summary = paymentSvc.Summarize(list),
                payments = list,
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Error loading payments: {ex.Message}" });
        }
    }
}
