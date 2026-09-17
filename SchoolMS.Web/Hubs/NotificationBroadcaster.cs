using Microsoft.AspNetCore.SignalR;
using SchoolMS.Domain;
using SchoolMS.Services;

namespace SchoolMS.Web.Hubs;

// Best-effort real-time push: the notification row is already committed to the DB by the time this
// runs (NotificationService.CreateNotification), so a failed/missed push just means the user sees it
// on next poll (TaskApiController.GetNotifications) instead of instantly — never a lost notification.
public class SignalRNotificationBroadcaster(IHubContext<NotificationHub> hub) : INotificationBroadcaster
{
    public void NotifyUser(int userId, Notification notif)
    {
        _ = hub.Clients.Group(NotificationHub.GroupName(userId)).SendAsync("notification", new {
            notificationId = notif.NotificationId,
            title = notif.Title,
            message = notif.Message,
            type = notif.Type,
            action = notif.Action,
            referenceId = notif.ReferenceId,
            createdAt = notif.CreatedAt,
        });
    }
}
