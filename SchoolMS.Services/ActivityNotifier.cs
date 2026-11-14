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

    public void AttendanceMarked(int studentId, DateTime date, string status, string? subject, int session = 1)
        => Safe(nameof(AttendanceMarked), () =>
        {
            var name = Name(studentId);
            // A second lecture on the same day is its own notification (not an overwrite of the first one).
            var lecture = session > 1 ? $"lecture {session}" : "";
            var inSubject = string.IsNullOrWhiteSpace(subject) ? (lecture == "" ? "" : $" ({lecture})")
                          : $" ({subject.Trim()}{(lecture == "" ? "" : ", " + lecture)})";
            var (kind, title, msg) = status switch
            {
                "Absent" => (ActivityType.Absent, "Absent", $"{name} was marked ABSENT on {Day(date)}{inSubject}."),
                "Late"   => (ActivityType.Late, "Late", $"{name} came LATE on {Day(date)}{inSubject}."),
                _        => (ActivityType.Present, "Present", $"{name} was marked present on {Day(date)}{inSubject}."),
            };
            repo.Upsert(studentId, kind, title, msg, session > 1 ? $"att:{date:yyyyMMdd}:s{session}" : $"att:{date:yyyyMMdd}", null);
        });

    // ── batch holiday ───────────────────────────────────────────────────────────────────────

    /// <summary>"No class today" for the batch (or, when the holiday is taken back, "class is on"). One row per student + batch + day, so re-saving updates it.</summary>
    public void BatchHoliday(int studentId, int batchId, string batchName, DateTime date, string? reason, bool cancelled)
        => Safe(nameof(BatchHoliday), () =>
        {
            var name = Name(studentId);
            var day = date.Date == DateTime.Today ? "today" : $"on {Day(date)}";
            var why = string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}.";
            var (title, msg) = cancelled
                ? ("Class is on", $"Update: the holiday is cancelled - {name}'s {batchName} class is on {day} ({Day(date)}) as usual.")
                : ("No class today", $"No class {day} ({Day(date)}) for {name}'s {batchName} batch.{why}");
            if (!cancelled && date.Date != DateTime.Today) title = "No class";
            repo.Upsert(studentId, ActivityType.Holiday, title, msg, $"holiday:{date:yyyyMMdd}:{batchId}", null);
        });

    // ── birthday ────────────────────────────────────────────────────────────────────────────

    /// <summary>The birthday wish, once per student per year (same key = no second notification when the page is opened again).</summary>
    public void BirthdayWish(int studentId, int year, string title, string message)
        => Safe(nameof(BirthdayWish), () => repo.Upsert(studentId, ActivityType.Birthday, title, message, $"bday:{year}", null));

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

    /// "Fee is pending" reminder from the Finance screen. One per student per day (pressing the button twice the same day
    /// updates the same notification instead of piling up).
    public void FeeDue(int studentId, decimal balance, DateTime date)
        => Safe(nameof(FeeDue), () =>
        {
            if (balance <= 0) return;
            repo.Upsert(studentId, ActivityType.Fee, "Fee reminder",
                $"Fee balance of ₹{balance:N0} is pending for {Name(studentId)}. Please pay at the earliest.", $"feedue:{date:yyyyMMdd}", null);
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
            // where to go: students and the teacher are told the classroom
            var room = string.IsNullOrWhiteSpace(first.ClassroomName) ? "" : $" Classroom: {first.ClassroomName.Trim()}.";
            var group = string.Join(" • ", new[] { className, sectionName, batchName }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));

            string studentMsg, teacherMsg, title;
            if (saved.Count == 1)
            {
                var when = $"{Day(first.LectureDate)}, {Clock(first.StartTime)} - {Clock(first.EndTime)}";
                title = isEdit ? "Lecture updated" : "New lecture";
                studentMsg = $"{first.SubjectName} lecture{who}: {when}.{room}{topic}";
                teacherMsg = $"{first.SubjectName} lecture for {group}: {when}.{room}{topic}";
            }
            else
            {
                var last = saved.Max(l => l.LectureDate);
                title = "New lectures scheduled";
                studentMsg = $"{saved.Count} {first.SubjectName} lectures{who}, {Clock(first.StartTime)} - {Clock(first.EndTime)}, from {Day(first.LectureDate)} to {Day(last)}.{room}";
                teacherMsg = $"{saved.Count} {first.SubjectName} lectures for {group}, {Clock(first.StartTime)} - {Clock(first.EndTime)}, from {Day(first.LectureDate)} to {Day(last)}.{room}";
            }

            var key = $"lec:{first.LectureId}:{first.SubjectName.Trim()}:{first.LectureDate:yyyyMMdd}{first.StartTime:hhmm}:{saved.Count}:{first.ClassroomId}";
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
    public int? FindTeacherUser(int facultyId)
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

    public List<StudentActivity> ForParent(string phone, int? studentId, int take = 100, IReadOnlyCollection<string>? hiddenSections = null)
        => repo.GetForParent(phone, studentId, take, PortalAccessService.KindsOf(hiddenSections));
    public int UnreadForParent(string phone, IReadOnlyCollection<string>? hiddenSections = null)
        => repo.GetUnreadForParent(phone, PortalAccessService.KindsOf(hiddenSections));
    public void MarkParentSeen(string phone) => repo.MarkSeen(phone);
}
