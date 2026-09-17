using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Hubs;

// Real-time push for staff notifications (Task/FeesDue reminders etc.) — one group per UserId so a
// user's notification only reaches their own connected device(s). Parent accounts don't use this: the
// Notifications table is UserId-scoped to staff only, matching TaskController's existing design.
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.UserId();
        if (userId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId.Value));
        await base.OnConnectedAsync();
    }

    public static string GroupName(int userId) => $"user-{userId}";
}
