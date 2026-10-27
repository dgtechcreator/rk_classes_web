using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

/// <summary>
/// Class notes and daily practice papers. Staff: module "class_notes" (View = list, Edit = upload / delete). A parent (or a student,
/// who logs in with the same portal) sees and downloads only what belongs to their child's class / medium / batch.
/// </summary>
[ApiController]
[Route("api/notes")]
public class NotesApiController(ClassNotesService svc, NoteFileStore files, ParentService parents) : ControllerBase
{
    static object Dto(ClassNote n) => new {
        noteId = n.NoteId, kind = n.Kind, title = n.Title, description = n.Description, classId = n.ClassId, className = n.ClassName,
        sectionId = n.SectionId, sectionName = n.SectionName, batchId = n.BatchId, batchName = n.BatchName, subjectName = n.SubjectName,
        fileName = n.FileName, fileSize = n.FileSize, paperDate = n.PaperDate, createdByName = n.CreatedByName, createdAt = n.CreatedAt,
    };

    [HttpGet]
    [ApiRequirePermission("class_notes")]
    public IActionResult List(int? classId, int? sectionId, int? batchId, string? kind, string? subject)
        => Ok(svc.Search(new ClassNoteFilter { ClassId = classId, SectionId = sectionId, BatchId = batchId, Kind = kind, Subject = subject }).Select(Dto));

    [HttpPost("upload")]
    [ApiRequirePermission("class_notes", requireEdit: true)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public IActionResult Upload([FromForm] NoteUploadReq req, IFormFile? file)
    {
        if (!NoteKind.IsValid(req.Kind)) return BadRequest(new { success = false, message = "Choose Notes or Practice paper." });
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest(new { success = false, message = "Enter a title." });
        if (req.ClassId <= 0) return BadRequest(new { success = false, message = "Select a class." });

        var (stored, error) = files.Save(file);
        if (error != null) return BadRequest(new { success = false, message = error });

        var id = svc.Add(new ClassNote {
            Kind = req.Kind!, Title = req.Title!, Description = req.Description, ClassId = req.ClassId,
            SectionId = req.SectionId is > 0 ? req.SectionId : null, BatchId = req.BatchId is > 0 ? req.BatchId : null,
            SubjectName = req.SubjectName, FileName = Path.GetFileName(file!.FileName), StoredName = stored!, FileSize = file.Length,
            PaperDate = (req.PaperDate ?? DateTime.Today).Date, CreatedBy = User.UserId(), CreatedByName = User.Identity?.Name,
        });
        return Ok(new { success = true, noteId = id, message = "Uploaded." });
    }

    [HttpPost("{id:int}/delete")]
    [ApiRequirePermission("class_notes", requireEdit: true)]
    public IActionResult Delete(int id)
        => svc.Delete(id) ? Ok(new { success = true }) : NotFound(new { success = false, message = "Not found." });

    /// <summary>Download: staff with the module right, or a parent / student of the note's class.</summary>
    [HttpGet("{id:int}/file")]
    public IActionResult Download(int id)
    {
        var u = User;
        if (u.Identity?.IsAuthenticated != true) return Unauthorized(new { error = "Login required." });
        var note = svc.GetById(id);
        if (note == null) return NotFound(new { error = "Not found." });

        bool allowed = u.UserType() == "staff"
            ? u.HasPerm("class_notes")
            : u.UserType() == "parent" && parents.GetChildren(u.Phone() ?? "").Any(c => ClassNotesService.AppliesTo(note, c.ClassId, c.SectionId, c.BatchId));
        if (!allowed) return StatusCode(403, new { error = "Access denied." });

        var fs = files.Open(note.StoredName);
        if (fs == null) return NotFound(new { error = "The file is no longer on the server." });
        return File(fs, NoteFileStore.ContentType(note.FileName), note.FileName);
    }
}

public class NoteUploadReq
{
    public string? Kind { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int ClassId { get; set; }
    public int? SectionId { get; set; }
    public int? BatchId { get; set; }
    public string? SubjectName { get; set; }
    public DateTime? PaperDate { get; set; }
}
