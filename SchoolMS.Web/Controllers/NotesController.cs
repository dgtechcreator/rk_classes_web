using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

/// <summary>Class notes + daily practice papers (web). Module "class_notes": View = list / download, Edit = upload / delete.</summary>
public class NotesController(ClassNotesService svc, NoteFileStore files, LookupService lookup, MastersService masters) : Controller
{
    [RequirePermission("class_notes")]
    public IActionResult Index(int? classId, int? sectionId, int? batchId, string? kind)
    {
        List<ClassNote> list;
        try { list = svc.Search(new ClassNoteFilter { ClassId = classId, SectionId = sectionId, BatchId = batchId, Kind = kind }); }
        catch { list = new(); }
        ViewBag.Classes = lookup.GetClasses();
        ViewBag.Sections = lookup.GetSections();
        ViewBag.Batches = lookup.GetBatches();
        ViewBag.Subjects = masters.GetSubjects().Select(s => s.SubjectName?.Trim() ?? "").Where(s => s != "").Distinct().OrderBy(s => s).ToList();
        ViewBag.CanEdit = HttpContext.Session.HasEditPerm("class_notes");
        ViewBag.ClassId = classId; ViewBag.SectionId = sectionId; ViewBag.BatchId = batchId; ViewBag.Kind = kind;
        return View(list);
    }

    [HttpPost]
    [RequirePermission("class_notes")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public IActionResult Upload(string? kind, string? title, string? description, int classId, int? sectionId, int? batchId,
        string? subjectName, DateTime? paperDate, IFormFile? file)
    {
        if (!HttpContext.Session.HasEditPerm("class_notes")) return RedirectToAction("AccessDenied", "Home");
        string? err = null;
        if (!NoteKind.IsValid(kind)) err = "Choose Notes or Practice paper.";
        else if (string.IsNullOrWhiteSpace(title)) err = "Enter a title.";
        else if (classId <= 0) err = "Select a class.";
        string? stored = null;
        if (err == null) (stored, err) = files.Save(file);
        if (err != null) { TempData["NoteError"] = err; return RedirectToAction("Index"); }

        svc.Add(new ClassNote {
            Kind = kind!, Title = title!, Description = description, ClassId = classId,
            SectionId = sectionId is > 0 ? sectionId : null, BatchId = batchId is > 0 ? batchId : null,
            SubjectName = subjectName, FileName = Path.GetFileName(file!.FileName), StoredName = stored!, FileSize = file.Length,
            PaperDate = (paperDate ?? DateTime.Today).Date, CreatedBy = HttpContext.Session.GetUserId(), CreatedByName = HttpContext.Session.GetString("FullName"),
        });
        TempData["NoteOk"] = "Uploaded. Parents of that class have been notified.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [RequirePermission("class_notes")]
    public IActionResult Delete(int id)
    {
        if (!HttpContext.Session.HasEditPerm("class_notes")) return RedirectToAction("AccessDenied", "Home");
        svc.Delete(id);
        TempData["NoteOk"] = "Deleted.";
        return RedirectToAction("Index");
    }

    [RequirePermission("class_notes")]
    public IActionResult Download(int id)
    {
        var n = svc.GetById(id);
        var fs = n == null ? null : files.Open(n.StoredName);
        if (n == null || fs == null) return NotFound();
        return File(fs, NoteFileStore.ContentType(n.FileName), n.FileName);
    }
}
