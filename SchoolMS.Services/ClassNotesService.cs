using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class ClassNotesService(ClassNotesRepo repo, ActivityNotifier? notify = null)
{
    public List<ClassNote> Search(ClassNoteFilter f) => repo.Search(f);
    public ClassNote? GetById(int id) => repo.GetById(id);

    public int Add(ClassNote n)
    {
        n.Normalize();
        var id = repo.Insert(n);
        n.NoteId = id;
        notify?.NotesPosted(n);
        return id;
    }

    public bool Delete(int id) => repo.SoftDelete(id);

    /// <summary>Does this note apply to a student of that class / medium / batch? (null section or batch on the note = all.)</summary>
    public static bool AppliesTo(ClassNote n, int? classId, int? sectionId, int? batchId)
        => classId == n.ClassId && (n.SectionId == null || n.SectionId == sectionId) && (n.BatchId == null || n.BatchId == batchId);
}

static class ClassNoteExt
{
    /// <summary>Normalises the fields a caller may leave blank.</summary>
    public static void Normalize(this ClassNote n)
    {
        n.Title = n.Title.Trim();
        n.Description = string.IsNullOrWhiteSpace(n.Description) ? null : n.Description.Trim();
        n.SubjectName = string.IsNullOrWhiteSpace(n.SubjectName) ? null : n.SubjectName.Trim();
    }
}
