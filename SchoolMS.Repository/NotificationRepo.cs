using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class NotificationRepo(CommonConnectivity db)
{
    public List<Notification> GetUserNotifications(int userId, string status = "")
    {
        string where = $"WHERE n.UserId={userId}";
        if (!string.IsNullOrEmpty(status))
            where += $" AND n.Status='{status}'";

        return db.Sql($@"
            SELECT TOP 50 * FROM Notifications n
            {where}
            ORDER BY n.CreatedAt DESC",
            r => new Notification {
                NotificationId = G.G<int>(r, "NotificationId"),
                UserId = G.G<int>(r, "UserId"),
                TaskId = G.G<int?>(r, "TaskId"),
                Title = G.G<string>(r, "Title") ?? "",
                Message = G.G<string>(r, "Message") ?? "",
                Type = G.G<string>(r, "Type") ?? "Task",
                Status = G.G<string>(r, "Status") ?? "Unread",
                CreatedAt = G.G<DateTime>(r, "CreatedAt"),
                ReadAt = G.G<DateTime?>(r, "ReadAt"),
                Action = G.G<string>(r, "Action"),
                ReferenceId = G.G<int?>(r, "ReferenceId")
            });
    }

    public int GetUnreadCount(int userId)
    {
        var result = db.Sql($"SELECT COUNT(*) as cnt FROM Notifications WHERE UserId={userId} AND Status='Unread'",
            r => G.G<int>(r, "cnt"));
        return result.FirstOrDefault();
    }

    public void CreateNotification(Notification notif)
    {
        db.Exec("sp_CreateNotification", new() {
            { "@UserId", notif.UserId },
            { "@TaskId", notif.TaskId },
            { "@Title", notif.Title },
            { "@Message", notif.Message },
            { "@Type", notif.Type },
            { "@Status", notif.Status },
            { "@Action", notif.Action },
            { "@ReferenceId", notif.ReferenceId }
        });
    }

    public void MarkAsRead(int notificationId)
    {
        db.Exec("sp_MarkNotificationRead", new() { { "@NotificationId", notificationId } });
    }

    public void DismissNotification(int notificationId)
    {
        db.Exec("sp_DismissNotification", new() { { "@NotificationId", notificationId } });
    }
}
