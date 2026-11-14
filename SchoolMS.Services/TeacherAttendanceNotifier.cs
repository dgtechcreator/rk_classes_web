using System.Diagnostics;
using SchoolMS.Domain;

namespace SchoolMS.Services;

/// <summary>
/// Tells a teacher when somebody else edits or deletes one of their teacher attendance entries: what it was, who did it and
/// when. Goes to the teacher's own login (staff notifications: web bell + app). Never fails the edit / delete.
/// </summary>
public class TeacherAttendanceNotifier(NotificationService notifs, ActivityNotifier activity, StaffNameService names, UserMgmtService users)
{
    static string Clock(TimeSpan? t) => t.HasValue ? DateTime.Today.Add(t.Value).ToString("h:mm tt") : "-";

    static string Describe(TeacherAttendance a)
    {
        var where = string.Join(" ", new[] { a.ClassName, a.SectionName, a.BatchName }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        var what = string.Join(", ", new[] { string.IsNullOrWhiteSpace(where) ? null : where, string.IsNullOrWhiteSpace(a.SubjectName) ? null : a.SubjectName.Trim(), a.InTime.HasValue ? $"{Clock(a.InTime)} - {Clock(a.OutTime)}" : null }.Where(x => x != null));
        return $"{a.AttendanceDate:dd MMM yyyy}" + (what.Length > 0 ? $" ({what})" : "");
    }

    string Who(int byUserId)
    {
        try { var n = names.DisplayName(byUserId); if (!string.IsNullOrWhiteSpace(n)) return n; } catch { }
        try { return users.GetById(byUserId)?.FullName ?? "the admin"; } catch { return "the admin"; }
    }

    void Send(TeacherAttendance rec, int byUserId, string title, string message)
    {
        try
        {
            var teacherUser = activity.FindTeacherUser(rec.FacultyId);
            if (teacherUser == null || teacherUser == byUserId) return;   // no login linked, or the teacher did it themself
            notifs.CreateNotification(new Notification {
                UserId = teacherUser.Value, Title = title, Message = message, Type = "TeacherAttendance",
                Status = "Unread", ReferenceId = rec.AttendanceId, Action = "/TeacherAttendance/Summary",
            });
        }
        catch (Exception ex) { Trace.TraceError("TeacherAttendanceNotifier failed: " + ex.Message); }
    }

    public void Deleted(TeacherAttendance rec, int byUserId)
        => Send(rec, byUserId, "Teacher attendance entry deleted",
                $"Your attendance entry of {Describe(rec)} was deleted by {Who(byUserId)} on {DateTime.Now:dd MMM yyyy, h:mm tt}.");

    public void Edited(TeacherAttendance before, TeacherAttendance after, int byUserId)
        => Send(before, byUserId, "Teacher attendance entry edited",
                $"Your attendance entry of {Describe(before)} was edited by {Who(byUserId)} on {DateTime.Now:dd MMM yyyy, h:mm tt}. Now: {Describe(after)}.");
}
