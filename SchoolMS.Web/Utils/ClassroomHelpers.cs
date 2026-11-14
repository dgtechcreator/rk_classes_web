using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Utils;

/// <summary>JSON for the classroom board and history, shared by the web pages and the mobile API.</summary>
public static class ClassroomHelpers
{
    static string T(TimeSpan t) => t.ToString(@"hh\:mm");

    static object LectureDto(Lecture l, DateTime now) => new
    {
        lectureId = l.LectureId, date = l.LectureDate.ToString("yyyy-MM-dd"), startTime = T(l.StartTime), endTime = T(l.EndTime),
        classroomId = l.ClassroomId, classroom = l.ClassroomName?.Trim(),
        subject = l.SubjectName.Trim(), teacher = l.FacultyName?.Trim(), facultyId = l.FacultyId,
        className = l.ClassName?.Trim(), sectionName = l.SectionName?.Trim(), batchName = l.BatchName?.Trim(), groupLabel = l.GroupLabel,
        topic = l.Topic, status = l.Status, statusNote = l.StatusNote,
        // for the history: ended / ongoing / upcoming, worked out from the clock (a Completed or Cancelled lecture keeps its own status)
        phase = l.Status == LectureStatus.Cancelled ? "cancelled"
              : l.Status == LectureStatus.Completed ? "done"
              : LectureService.IsPast(l, now) ? "ended"
              : (l.LectureDate.Date == now.Date && l.StartTime <= now.TimeOfDay) ? "ongoing" : "upcoming",
    };

    public static object BoardPayload(ClassroomService svc, DateTime? date)
    {
        var now = DateTime.Now;
        var at = date.HasValue && date.Value.Date != now.Date ? date.Value.Date.AddHours(8) : now;
        var rooms = svc.Board(at);
        return new
        {
            date = at.ToString("yyyy-MM-dd"), isToday = at.Date == now.Date, now = now.ToString("HH:mm"),
            busy = rooms.Count(r => r.State == "busy"), free = rooms.Count(r => r.State != "busy"), total = rooms.Count,
            rooms = rooms.Select(r => new
            {
                id = r.Room.ClassroomId, name = r.Room.Name.Trim(), capacity = r.Room.Capacity, location = r.Room.Location,
                state = r.State,
                busyUntil = r.BusyUntil.HasValue ? T(r.BusyUntil.Value) : null,
                current = r.Current == null ? null : LectureDto(r.Current, now),
                next = r.Next == null ? null : LectureDto(r.Next, now),
                day = r.Day.Select(l => LectureDto(l, now)),
            }),
        };
    }

    public static object HistoryPayload(ClassroomService svc, int? roomId, DateTime? from, DateTime? to, string? q)
    {
        var now = DateTime.Now;
        var (f, t) = LectureHelpers.Range(from ?? now.Date.AddDays(-30), to ?? now.Date.AddDays(7));
        var list = svc.History(roomId, f, t, q);
        return new
        {
            from = f.ToString("yyyy-MM-dd"), to = t.ToString("yyyy-MM-dd"), count = list.Count,
            hours = Math.Round(list.Where(l => l.Status != LectureStatus.Cancelled).Sum(l => l.DurationHours), 2),
            lectures = list.Select(l => LectureDto(l, now)),
        };
    }
}
