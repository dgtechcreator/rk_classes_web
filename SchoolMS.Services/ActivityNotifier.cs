using System.Diagnostics;
using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>
/// Tells people about things that happen: a student's parents hear about attendance, marks, fees and lectures; a teacher
/// hears when a lecture is given to them. Called from the services that do the saving, so the website and the mobile app
/// both trigger it. A failed notification must never fail the save that caused it - every public method swallows errors.
/// </summary>
public class ActivityNotifier(ActivityRepo repo, NotificationService staffNotifs, UserMgmtService users, TeacherAccountLinker linker)
{
    static string Day(DateTime d) => d.ToString("ddd, d MMM");
    static string Clock(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt");

    static void Safe(string what, Action a)
    {
        try { a(); }
        catch (Exception ex) { Trace.TraceError($"ActivityNotifier.{what} failed: {ex.Message}"); }
    }

    string Name(int studentId) => repo.GetStudentLabel(studentId)?.display is { Length: > 0 } n ? n : "Your child";

    // ── attendance ──────────────────────────────────────────────────────────────────────────

    public void AttendanceMarked(int studentId, DateTime date, string status, string? subject)
        => Safe(nameof(AttendanceMarked), () =>
        {
            var name = Name(studentId);
            var inSubject = string.IsNullOrWhiteSpace(subject) ? "" : $" ({subject.Trim()})";
            var (kind, title, msg) = status switch
            {
                "Absent" => (ActivityType.Absent, "Absent", $"{name} was marked ABSENT on {Day(date)}{inSubject}."),
                "Late"   => (ActivityType.Late, "Late", $"{name} came LATE on {Day(date)}{inSubject}."),
                _        => (ActivityType.Present, "Present", $"{name} was marked present on {Day(date)}{inSubject}."),
            };
            repo.Upsert(studentId, kind, title, msg, $"att:{date:yyyyMMdd}", null);
        });

    // ── marks ───────────────────────────────────────────────────────────────────────────────

    public void MarksEntered(int studentId, int subjectId, string examName, DateTime? testDate, decimal? marks, int maxMarks, bool isAbsent)
        => Safe(nameof(MarksEntered), () =>
        {
            if (!isAbsent && marks == null) return;   // a blank cell is not an event
            var name = Name(studentId);
            var subject = repo.GetSubjectName(subjectId)?.Trim() ?? "";
            var test = string.IsNullOrWhiteSpace(examName) ? "test" : examName.Trim();
            var what = subject == "" ? test : $"{subject} - {test}";
            var msg = isAbsent
                ? $"{name} was absent for {what}."
                : $"{name} scored {marks:0.##}/{maxMarks} in {what}.";
            repo.Upsert(studentId, ActivityType.Marks, isAbsent ? "Test missed" : "Marks entered", msg,
                $"marks:{subjectId}:{test}:{testDate:yyyyMMdd}", null);
        });

    // ── fees ────────────────────────────────────────────────────────────────────────────────

    public void FeeReceived(int studentId, decimal amount, DateTime date, int paymentId)
        => Safe(nameof(FeeReceived), () =>
        {
            if (amount <= 0) return;
            repo.Upsert(studentId, ActivityType.Fee, "Fee received",
                $"Fee payment of ₹{amount:N0} received for {Name(studentId)} on {Day(date)}.", $"fee:{paymentId}", paymentId);
        });

    // ── notes / practice papers ───────────────────────────────────────────────────────────

    public void NotesPosted(ClassNote n)
        => Safe(nameof(NotesPosted), () =>
        {
            var practice = n.Kind == NoteKind.Practice;
            var subject = string.IsNullOrWhiteSpace(n.SubjectName) ? "" : $"{n.SubjectName.Trim()}: ";
            var msg = practice
                ? $"{subject}practice paper for {Day(n.PaperDate)} - {n.Title}."
                : $"{subject}{n.Title}.";
            repo.InsertForGroup(n.ClassId, n.SectionId, n.BatchId, practice ? ActivityType.Practice : ActivityType.Notes,
                practice ? "Daily practice paper" : "New notes", msg, $"note:{n.NoteId}", n.NoteId);
        });

    // ── lectures ────────────────────────────────────────────────────────────────────────────

    /// <summary>A lecture (or a repeating series of them) was scheduled or changed: tell the class and the teacher.</summary>
    public void LecturesSaved(List<Lecture> saved, bool isEdit)
        => Safe(nameof(LecturesSaved), () =>
        {
            if (saved.Count == 0) return;
            var first = saved.OrderBy(l => l.LectureDate).ThenBy(l => l.StartTime).First();
            var (className, sectionName, batchName, teacherName) = repo.GetLectureLabels(first.ClassId, first.SectionId, first.BatchId, first.FacultyId);
            var who = string.IsNullOrWhiteSpace(teacherName) ? "" : $" by {teacherName.Trim()}";
            var topic = string.IsNullOrWhiteSpace(first.Topic) ? "" : $" Topic: {first.Topic.Trim()}.";
            var group = string.Join(" • ", new[] { className, sectionName, batchName }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));

            string studentMsg, teacherMsg, title;
            if (saved.Count == 1)
            {
                var when = $"{Day(first.LectureDate)}, {Clock(first.StartTime)} - {Clock(first.EndTime)}";
                title = isEdit ? "Lecture updated" : "New lecture";
                studentMsg = $"{first.SubjectName} lecture{who}: {when}.{topic}";
                teacherMsg = $"{first.SubjectName} lecture for {group}: {when}.{topic}";
            }
            else
            {
                var last = saved.Max(l => l.LectureDate);
                title = "New lectures scheduled";
                studentMsg = $"{saved.Count} {first.SubjectName} lectures{who}, {Clock(first.StartTime)} - {Clock(first.EndTime)}, from {Day(first.LectureDate)} to {Day(last)}.";
                teacherMsg = $"{saved.Count} {first.SubjectName} lectures for {group}, {Clock(first.StartTime)} - {Clock(first.EndTime)}, from {Day(first.LectureDate)} to {Day(last)}.";
            }

            var key = $"lec:{first.LectureId}:{first.SubjectName.Trim()}:{first.LectureDate:yyyyMMdd}{first.StartTime:hhmm}:{saved.Count}";
            repo.InsertForGroup(first.ClassId, first.SectionId, first.BatchId, isEdit ? ActivityType.LectureUpdate : ActivityType.Lecture,
                title, studentMsg, key, first.LectureId > 0 ? first.LectureId : null);

            NotifyTeacher(first.FacultyId, isEdit ? "Lecture updated" : "Lecture assigned", teacherMsg, first.LectureId);
        });

    /// <summary>Cancelled / completed (SetStatus) or deleted: tell the class and the teacher.</summary>
    public void LectureStatusChanged(Lecture l, string status, string? note)
        => Safe(nameof(LectureStatusChanged), () =>
        {
            if (status == LectureStatus.Scheduled) return;   // re-opened: nothing to announce
            var when = $"{Day(l.LectureDate)}, {Clock(l.StartTime)}";
            var done = status == LectureStatus.Completed;
            var reason = !done && !string.IsNullOrWhiteSpace(note) ? $" Reason: {note.Trim()}." : "";
            var title = done ? "Lecture completed" : "Lecture cancelled";
            var msg = $"{l.SubjectName} lecture on {when} was {(done ? "completed" : "CANCELLED")}.{reason}";
            repo.InsertForGroup(l.ClassId, l.SectionId, l.BatchId, done ? ActivityType.LectureDone : ActivityType.LectureCancel,
                title, msg, $"lecstat:{l.LectureId}:{status}", l.LectureId);
            if (!done) NotifyTeacher(l.FacultyId, title, $"{l.SubjectName} lecture for {l.GroupLabel} on {when} was cancelled.{reason}", l.LectureId);
        });

    public void LectureDeleted(Lecture l, int count = 1)
        => Safe(nameof(LectureDeleted), () =>
        {
            if (l.Status == LectureStatus.Cancelled || LectureService.IsPast(l, DateTime.Now)) return;   // already announced / nothing to cancel
            var when = $"{Day(l.LectureDate)}, {Clock(l.StartTime)}";
            var text = count > 1
                ? $"{count} {l.SubjectName} lectures from {when} onwards were cancelled."
                : $"{l.SubjectName} lecture on {when} was cancelled.";
            repo.InsertForGroup(l.ClassId, l.SectionId, l.BatchId, ActivityType.LectureCancel, "Lecture cancelled", text, $"lecdel:{l.LectureId}", l.LectureId);
            NotifyTeacher(l.FacultyId, "Lecture cancelled", count > 1 ? $"{text} ({l.GroupLabel})" : $"{l.SubjectName} lecture for {l.GroupLabel} on {when} was cancelled.", l.LectureId);
        });

    void NotifyTeacher(int facultyId, string title, string message, int lectureId)
    {
        var userId = FindTeacherUser(facultyId);
        if (userId == null) return;
        staffNotifs.CreateNotification(new Notification
        {
            UserId = userId.Value, Title = title, Message = message, Type = "Lecture",
            Status = "Unread", ReferenceId = lectureId > 0 ? lectureId : null,
        });
    }

    /// <summary>The login of a teacher: the one the admin linked on the Users form, else the automatic phone / name match.</summary>
    int? FindTeacherUser(int facultyId)
    {
        try { var linked = repo.GetUserIdByFaculty(facultyId); if (linked != null) return linked; }
        catch { /* Users.FacultyId (SQL/67) not there yet - fall back to the automatic match */ }
        try
        {
            return users.GetAll().Where(u => u.IsActive)
                .FirstOrDefault(u => linker.FindFaculty(u.UserId)?.FacultyId == facultyId)?.UserId;
        }
        catch { return null; }
    }

    // ── reading (parent side) ───────────────────────────────────────────────────────────────

    public List<StudentActivity> ForParent(string phone, int? studentId, int take = 100) => repo.GetForParent(phone, studentId, take);
    public int UnreadForParent(string phone) => repo.GetUnreadForParent(phone);
    public void MarkParentSeen(string phone) => repo.MarkSeen(phone);
}
