using Microsoft.AspNetCore.Mvc;
using SchoolMS.Domain;
using SchoolMS.Services;
using SchoolMS.Web.Filters;

namespace SchoolMS.Web.Controllers;

[RequireLogin]
public class TaskController(TaskService taskSvc, NotificationService notifSvc) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        int userId = HttpContext.Session.GetUserId() ?? 0;
        int? roleId = HttpContext.Session.GetInt32("RoleId");

        // Admin sees all tasks, others see only their own
        var tasks = roleId == 1 ? taskSvc.GetAllTasks() : taskSvc.GetUserTasks(userId);
        return View(tasks);
    }

    [HttpPost]
    public IActionResult CreateTask(string title, string description, int? studentId, string dueDate)
    {
        try
        {
            int userId = HttpContext.Session.GetUserId() ?? 0;

            // Validate inputs
            if (string.IsNullOrWhiteSpace(title))
                return Json(new { success = false, message = "Title is required" });

            if (string.IsNullOrWhiteSpace(dueDate))
                return Json(new { success = false, message = "Due date is required" });

            // Parse date
            if (!DateTime.TryParseExact(dueDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
                return Json(new { success = false, message = "Invalid date format" });

            // studentId ke liye: agar empty ya 0 hai toh NULL banao
            if (studentId == 0)
                studentId = null;

            var task = new TaskItem
            {
                Title = title.Trim(),
                Description = description?.Trim() ?? "",
                StudentId = studentId,
                UserId = userId,
                Priority = "Medium",
                Category = "General",
                DueDate = parsedDate,
                Status = "Pending",
                IsCompleted = false  // Explicitly set
            };

            taskSvc.SaveTask(task);
            return Json(new { success = true, message = "Task created successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult CompleteTask(int taskId)
    {
        try
        {
            int userId = HttpContext.Session.GetUserId() ?? 0;
            taskSvc.CompleteTask(taskId);

            // Mark task notification as read instead of creating new one
            notifSvc.MarkAllAsRead(userId);

            return Json(new { success = true, message = "Task marked as complete" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult DeleteTask(int taskId)
    {
        try
        {
            taskSvc.DeleteTask(taskId);
            int userId = HttpContext.Session.GetUserId() ?? 0;
            if (userId > 0)
            {
                notifSvc.MarkAllAsRead(userId);
            }
            return Json(new { success = true, message = "Task deleted" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult GetNotifications()
    {
        int userId = HttpContext.Session.GetUserId() ?? 0;
        var notifications = notifSvc.GetUserNotifications(userId);
        var tasks = taskSvc.GetPendingTasks(userId);
        var unreadCount = notifSvc.GetUnreadCount(userId) + (tasks?.Count ?? 0);

        return Json(new { notifications, tasks, unreadCount });
    }

    [HttpPost]
    public IActionResult MarkNotificationAsRead(int notificationId)
    {
        try
        {
            notifSvc.MarkAsRead(notificationId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult DismissNotification(int notificationId)
    {
        try
        {
            notifSvc.DismissNotification(notificationId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
