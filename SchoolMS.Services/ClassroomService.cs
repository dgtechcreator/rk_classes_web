using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

/// <summary>Classrooms master + the live board (which room is free / engaged now and until when) + room history.</summary>
public class ClassroomService(ClassroomRepo repo, LectureRepo lectures)
{
    public List<Classroom> GetAll(bool includeInactive = false) => repo.GetAll(includeInactive);
    public Classroom? GetById(int id) => repo.GetById(id);

    public (string? error, int id) Save(Classroom c, int by)
    {
        var name = (c.Name ?? "").Trim();
        if (name == "") return ("Enter the classroom name.", 0);
        if (name.Length > 80) return ("The name is too long.", 0);
        if (c.Capacity is < 0 or > 5000) return ("Enter a valid capacity.", 0);
        if (repo.NameExists(name, c.ClassroomId)) return ($"A classroom named '{name}' already exists.", 0);
        c.Name = name;
        return (null, repo.Save(c, by));
    }

    public string? Delete(int id, int by)
    {
        var n = repo.UpcomingLectureCount(id);
        if (n > 0) return $"This classroom still has {n} upcoming lecture{(n == 1 ? "" : "s")}. Move or cancel them first, or switch the classroom off instead.";
        repo.SoftDelete(id, by);
        return null;
    }

    /// <summary>
    /// Every active classroom for a day. For today: "busy" while a Scheduled lecture is on (back-to-back lectures are chained
    /// into one "engaged until"), otherwise "free" with the next lecture. For another date nothing is "busy" - the day plan is returned.
    /// </summary>
    public List<ClassroomStatus> Board(DateTime at)
    {
        var date = at.Date;
        var t = at.TimeOfDay;
        bool isToday = date == DateTime.Today;
        var rooms = repo.GetAll(false);
        var day = lectures.Search(new LectureFilter { From = date, To = date, WithClassroom = true })
            .Where(l => l.Status != LectureStatus.Cancelled).ToList();

        var result = new List<ClassroomStatus>();
        foreach (var room in rooms)
        {
            var mine = day.Where(l => l.ClassroomId == room.ClassroomId).OrderBy(l => l.StartTime).ToList();
            var st = new ClassroomStatus { Room = room, Day = mine };
            if (isToday)
            {
                var cur = mine.FirstOrDefault(l => l.Status == LectureStatus.Scheduled && l.StartTime <= t && t < l.EndTime);
                if (cur != null)
                {
                    st.State = "busy";
                    st.Current = cur;
                    var until = cur.EndTime;
                    foreach (var n in mine.Where(l => l.Status == LectureStatus.Scheduled && l.StartTime > cur.StartTime).OrderBy(l => l.StartTime))
                        if (n.StartTime <= until) until = n.EndTime > until ? n.EndTime : until;
                    st.BusyUntil = until;
                }
                st.Next = mine.FirstOrDefault(l => l.Status == LectureStatus.Scheduled && l.StartTime > t && (cur == null || l.LectureId != cur.LectureId));
            }
            else st.Next = mine.FirstOrDefault();
            result.Add(st);
        }
        return result.OrderBy(r => r.State == "busy" ? 0 : 1).ThenBy(r => r.Room.Name).ToList();
    }

    /// <summary>Past and future lectures of one room (or all rooms): who taught what, to which class / medium / batch, and the status.</summary>
    public List<Lecture> History(int? roomId, DateTime from, DateTime to, string? q)
    {
        var list = lectures.Search(new LectureFilter { From = from, To = to, ClassroomId = roomId, WithClassroom = true });
        if (!string.IsNullOrWhiteSpace(q))
        {
            var words = q.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            list = list.Where(l =>
            {
                var text = string.Join(' ', new[] { l.ClassroomName, l.FacultyName, l.SubjectName, l.Topic, l.GroupLabel, l.Status }.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();
                return words.All(text.Contains);
            }).ToList();
        }
        return list.OrderByDescending(l => l.LectureDate).ThenByDescending(l => l.StartTime).ToList();
    }
}
