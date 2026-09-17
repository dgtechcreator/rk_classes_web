using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Auth;

namespace SchoolMS.Web.Controllers.Api;

[ApiController]
[Route("api/tasks")]
[ApiRequireStaff]
public class TaskApiController(TaskService taskSvc, NotificationService notifSvc) : ControllerBase
{
    public record CreateTaskReq(string Title, string Description, int? StudentId, string DueDate, int? AssignToUserId = null);

    [HttpGet]
    public IActionResult Index()
    {
        int userId = User.UserId() ?? 0;
        int? roleId = User.RoleId();

        var tasks = roleId == 1 ? taskSvc.GetAllTasks() : taskSvc.GetUserTasks(userId);
        return Ok(tasks);
    }

    [HttpPost("create")]
    public IActionResult CreateTask([FromBody] CreateTaskReq req)
    {
        try
        {
            int userId = User.UserId() ?? 0;

            if (string.IsNullOrWhiteSpace(req.Title))
                return Ok(new { success = false, message = "Title is required" });

            if (string.IsNullOrWhiteSpace(req.DueDate))
                return Ok(new { success = false, message = "Due date is required" });

            if (!DateTime.TryParseExact(req.DueDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
                return Ok(new { success = false, message = "Invalid date format" });

            int? studentId = req.StudentId == 0 ? null : req.StudentId;

            var task = new TaskItem {
                Title = req.Title.Trim(),
                Description = req.Description?.Trim() ?? "",
                StudentId = studentId,
                UserId = req.AssignToUserId ?? userId,
                Priority = "Medium",
                Category = "General",
                DueDate = parsedDate,
                Status = "Pending",
                IsCompleted = false
            };

            taskSvc.SaveTask(task);
            return Ok(new { success = true, message = "Task created successfully" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{taskId:int}/complete")]
    public IActionResult CompleteTask(int taskId)
    {
        try
        {
            int userId = User.UserId() ?? 0;
            taskSvc.CompleteTask(taskId);
            notifSvc.MarkAllAsRead(userId);
            return Ok(new { success = true, message = "Task marked as complete" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("{taskId:int}/delete")]
    public IActionResult DeleteTask(int taskId)
    {
        try
        {
            taskSvc.DeleteTask(taskId);
            int userId = User.UserId() ?? 0;
            if (userId > 0) notifSvc.MarkAllAsRead(userId);
            return Ok(new { success = true, message = "Task deleted" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("notifications")]
    public IActionResult GetNotifications()
    {
        int userId = User.UserId() ?? 0;
        var notifications = notifSvc.GetUserNotifications(userId);
        var tasks = taskSvc.GetPendingTasks(userId);
        var unreadCount = notifSvc.GetUnreadCount(userId) + (tasks?.Count ?? 0);

        return Ok(new { notifications, tasks, unreadCount });
    }

    [HttpPost("notifications/{notificationId:int}/read")]
    public IActionResult MarkNotificationAsRead(int notificationId)
    {
        try
        {
            notifSvc.MarkAsRead(notificationId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("notifications/{notificationId:int}/dismiss")]
    public IActionResult DismissNotification(int notificationId)
    {
        try
        {
            notifSvc.DismissNotification(notificationId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }
}
