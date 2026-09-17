using SchoolMS.Domain;

namespace SchoolMS.Services;

// Implemented in SchoolMS.Web (SignalR needs ASP.NET Core hosting types the Services layer can't
// reference) and injected here — keeps NotificationService free of any web-layer dependency.
public interface INotificationBroadcaster
{
    void NotifyUser(int userId, Notification notif);
}
