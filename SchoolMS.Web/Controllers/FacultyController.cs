using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class FacultyController(FacultyService svc, LookupService lookup) : Controller
{
    public IActionResult Index(int page=1, string? search=null, string? status=null, int? designationId=null)
    {
        var (data, total) = svc.GetAll(search, status, designationId, page, 15);
        ViewBag.Designations  = svc.GetDesignations();
        ViewBag.Search        = search;
        ViewBag.Status        = status;
        ViewBag.DesignationId = designationId;
        ViewBag.Page          = page;
        ViewBag.Total         = total;
        ViewBag.TotalPages    = Math.Max(1,(int)Math.Ceiling((double)total/15));
        return View(data);
    }

    public IActionResult Create()
    {
        ViewBag.Designations = svc.GetDesignations();
        ViewBag.Classes      = lookup.GetClasses();
        ViewBag.Sections     = lookup.GetSections();
        return View("Form", new Faculty { DateOfJoining = DateTime.Today });
    }

    public IActionResult Edit(int id)
    {
        var f = svc.GetById(id);
        if (f == null) return NotFound();
        ViewBag.Designations = svc.GetDesignations();
        ViewBag.Classes      = lookup.GetClasses();
        ViewBag.Sections     = lookup.GetSections();
        ViewBag.Subjects     = svc.GetSubjects(id);
        return View("Form", f);
    }

    [HttpPost]
    public IActionResult Save(Faculty model, IFormFile? ProfilePic)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        string? picPath = null;
        if (ProfilePic != null && ProfilePic.Length > 0)
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "faculty");
            Directory.CreateDirectory(dir);
            var fn = $"{Guid.NewGuid()}{Path.GetExtension(ProfilePic.FileName)}";
            using var fs = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            ProfilePic.CopyTo(fs);
            picPath = $"/images/faculty/{fn}";
        }
        var id = svc.Save(model, picPath, uid);
        TempData["Success"] = "Faculty member saved successfully.";
        return RedirectToAction("Details", new { id });
    }

    public IActionResult Details(int id)
    {
        var f = svc.GetById(id);
        if (f == null) return NotFound();
        ViewBag.Subjects = svc.GetSubjects(id);
        return View(f);
    }
}
