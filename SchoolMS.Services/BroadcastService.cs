using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class BroadcastReq
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    /// <summary>everyone | students | staff | teachers | class | pick_students | pick_staff</summary>
    public string Audience { get; set; } = "";
    public int? ClassId { get; set; }
    public int? SectionId { get; set; }
    public int? BatchId { get; set; }
    public List<int> StudentIds { get; set; } = new();
    public List<int> UserIds { get; set; } = new();
}

/// <summary>
/// "Send Notification" (admin): one message to everybody, to all students / all staff / all teachers, to one class (+ medium, batch),
/// or to particular students / particular staff. Students and parents see it in their notifications (per student), staff get a
/// normal notification (with the live push).
/// </summary>
public class BroadcastService(ActivityRepo activity, NotificationService staffNotifs, StudentService students, UserMgmtService users)
{
    public static readonly string[] Audiences = { "everyone", "students", "staff", "teachers", "class", "pick_students", "pick_staff" };

    public (string? error, int students, int staff) Send(BroadcastReq r, int byUserId)
    {
        var title = (r.Title ?? "").Trim();
        var message = (r.Message ?? "").Trim();
        if (title == "") return ("Write a title.", 0, 0);
        if (title.Length > 150) return ("The title is too long (150 characters at most).", 0, 0);
        if (message == "") return ("Write the message.", 0, 0);
        if (message.Length > 900) return ("The message is too long (900 characters at most).", 0, 0);
        var aud = (r.Audience ?? "").ToLowerInvariant();
        if (!Audiences.Contains(aud)) return ("Choose who should get it.", 0, 0);
        if (aud == "class" && (r.ClassId ?? 0) <= 0) return ("Choose the class.", 0, 0);
        if (aud == "pick_students" && r.StudentIds.Count == 0) return ("Choose at least one student.", 0, 0);
        if (aud == "pick_staff" && r.UserIds.Count == 0) return ("Choose at least one staff member.", 0, 0);

        var key = "notice:" + Guid.NewGuid().ToString("N");
        int sCount = 0, uCount = 0;

        // students / parents
        if (aud is "everyone" or "students")
        {
            var (list, _) = students.GetAll(1, 100000, null, null, null, null, null, "Active");
            sCount = activity.InsertForStudents(list.Select(s => s.StudentId), ActivityType.Notice, title, message, key);
        }
        else if (aud == "class")
            sCount = activity.InsertForGroup(r.ClassId!.Value, r.SectionId, r.BatchId, ActivityType.Notice, title, message, key, null);
        else if (aud == "pick_students")
            sCount = activity.InsertForStudents(r.StudentIds, ActivityType.Notice, title, message, key);

        // staff
        if (aud is "everyone" or "staff" or "teachers" or "pick_staff")
        {
            var all = users.GetAll().Where(u => u.IsActive && u.UserId != byUserId).ToList();
            IEnumerable<AppUser> targets = aud switch
            {
                "teachers" => all.Where(u => (u.RoleName ?? "").Contains("teacher", StringComparison.OrdinalIgnoreCase)),
                "pick_staff" => all.Where(u => r.UserIds.Contains(u.UserId)),
                _ => all,
            };
            foreach (var u in targets)
            {
                try
                {
                    staffNotifs.CreateNotification(new Notification { UserId = u.UserId, Title = title, Message = message, Type = "Announcement", Status = "Unread" });
                    uCount++;
                }
                catch { /* one failed row must not stop the rest */ }
            }
        }
        if (sCount == 0 && uCount == 0) return ("There was nobody to send it to for this choice.", 0, 0);
        return (null, sCount, uCount);
    }

    /// <summary>Active staff logins for the "particular staff" picker.</summary>
    public List<(int id, string name, string role)> StaffList(int exceptUserId)
        => users.GetAll().Where(u => u.IsActive && u.UserId != exceptUserId).OrderBy(u => u.FullName)
            .Select(u => (u.UserId, u.FullName ?? u.Username, u.RoleName ?? "")).ToList();

    /// <summary>Students matching a typed search (name, father, mother, admission no ...) for the "particular students" picker.</summary>
    public List<(int id, string label)> FindStudents(string? q, int take = 40)
    {
        var (list, _) = students.GetAll(1, take, string.IsNullOrWhiteSpace(q) ? null : q.Trim(), null, null, null, null, "Active");
        return list.Select(s => (s.StudentId, $"{s.DisplayName} · {s.ClassName}{(string.IsNullOrWhiteSpace(s.SectionName) ? "" : " · " + s.SectionName)}")).ToList();
    }
}
