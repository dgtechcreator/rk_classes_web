using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class NotificationService(NotificationRepo repo, INotificationBroadcaster broadcaster)
{
    public List<Notification> GetUserNotifications(int userId, string status = "")
        => repo.GetUserNotifications(userId, status);

    public List<Notification> GetUnreadNotifications(int userId)
        => repo.GetUserNotifications(userId, "Unread");

    public int GetUnreadCount(int userId)
        => repo.GetUnreadCount(userId);

    public void CreateNotification(Notification notif)
    {
        repo.CreateNotification(notif);
        broadcaster.NotifyUser(notif.UserId, notif);
    }

    public void MarkAsRead(int notificationId)
        => repo.MarkAsRead(notificationId);

    public void DismissNotification(int notificationId)
        => repo.DismissNotification(notificationId);

    public void MarkAllAsRead(int userId)
    {
        var unread = repo.GetUserNotifications(userId, "Unread");
        foreach (var notif in unread)
        {
            repo.MarkAsRead(notif.NotificationId);
        }
    }
}
