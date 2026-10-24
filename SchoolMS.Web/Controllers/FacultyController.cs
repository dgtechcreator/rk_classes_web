using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class FacultyController(FacultyService svc, LookupService lookup, ProfilePhotoStore photos) : Controller
{
    public IActionResult Index(int page=1, string? search=null, string? status="Active", int? designationId=null)
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
    public IActionResult Save(Faculty model, IFormFile? ProfilePic, bool RemovePhoto = false)
    {
        int uid = HttpContext.Session.GetUserId() ?? 1;
        string? picPath = null;
        if (ProfilePic != null && ProfilePic.Length > 0)
        {
            var (saved, err) = photos.Save(ProfilePic, "faculty");
            if (err != null)
            {
                TempData["Error"] = err;
                return model.FacultyId == 0 ? RedirectToAction("Create") : RedirectToAction("Edit", new { id = model.FacultyId });
            }
            picPath = saved;
        }
        var oldPic = model.FacultyId == 0 ? null : svc.GetById(model.FacultyId)?.ProfilePicPath;
        var id = svc.Save(model, picPath, uid);
        if (id > 0 && model.FacultyId != 0)
        {
            if (picPath != null) photos.Delete(oldPic);
            else if (RemovePhoto) { svc.SetPhoto(id, null); photos.Delete(oldPic); }
        }
        TempData["Success"] = "Faculty member saved successfully.";
        return RedirectToAction("Details", new { id });
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        svc.Delete(id);
        TempData["Success"] = "Faculty member marked as Inactive.";
        return RedirectToAction("Index");
    }

    public IActionResult Details(int id)
    {
        var f = svc.GetById(id);
        if (f == null) return NotFound();
        ViewBag.Subjects = svc.GetSubjects(id);
        return View(f);
    }
}
