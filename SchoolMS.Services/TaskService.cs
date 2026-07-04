using SchoolMS.Domain;
using SchoolMS.Repository;

namespace SchoolMS.Services;

public class TaskService(TaskRepo repo, NotificationRepo notifRepo)
{
    public List<TaskItem> GetAllTasks() => repo.GetAllTasks();
    public List<TaskItem> GetUserTasks(int userId, string status = "") => repo.GetUserTasks(userId, status);
    public List<TaskItem> GetPendingTasks(int userId) => repo.GetUserTasks(userId, "Pending");
    public List<TaskItem> GetOverdueTasks(int userId) => repo.GetUserTasks(userId, "Overdue");
    public TaskItem? GetTaskById(int taskId) => repo.GetTaskById(taskId);
    public int GetPendingTaskCount(int userId) => repo.GetPendingTaskCount(userId);

    public void SaveTask(TaskItem task)
    {
        // Update status based on due date
        if (!task.IsCompleted)
        {
            task.Status = task.DueDate.Date < DateTime.Today ? "Overdue" : "Pending";
        }

        repo.SaveTask(task);
    }

    public void CompleteTask(int taskId)
    {
        repo.CompleteTask(taskId);
    }

    public void DeleteTask(int taskId)
    {
        repo.DeleteTask(taskId);
    }

    public void CreateFeesDueTask(int studentId, int userId, string studentName, DateTime dueDate)
    {
        var task = new TaskItem
        {
            Title = $"Fee Due - {studentName}",
            Description = $"Collect fees from {studentName}",
            StudentId = studentId,
            UserId = userId,
            Priority = "High",
            Category = "FeesDue",
            DueDate = dueDate,
            Status = "Pending"
        };
        repo.SaveTask(task);

        // Create notification
        notifRepo.CreateNotification(new Notification
        {
            UserId = userId,
            Title = $"Fee Due Reminder",
            Message = $"Fee is due for {studentName} on {dueDate:dd MMM yyyy}",
            Type = "FeesDue",
            ReferenceId = studentId,
            Action = $"/Student/Profile360?studentId={studentId}"
        });
    }
}
