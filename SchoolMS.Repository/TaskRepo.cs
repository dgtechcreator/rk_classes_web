using SchoolMS.DB;
using SchoolMS.Domain;
using G = SchoolMS.DB.CommonConnectivity;

namespace SchoolMS.Repository;

public class TaskRepo(CommonConnectivity db)
{
    public List<TaskItem> GetAllTasks()
    {
        return db.Sql(@"
            SELECT t.*, ISNULL(s.FullName,'') as StudentName, ISNULL(s.AdmissionNo,'') as AdmissionNo, ISNULL(u.FullName,'') as CreatedByName
            FROM Tasks t
            LEFT JOIN Students s ON s.StudentId=t.StudentId
            LEFT JOIN Users u ON u.UserId=t.UserId
            ORDER BY t.DueDate ASC",
            r => new TaskItem {
                TaskId = G.G<int>(r, "TaskId"),
                Title = G.G<string>(r, "Title") ?? "",
                Description = G.G<string>(r, "Description"),
                StudentId = G.G<int?>(r, "StudentId"),
                UserId = G.G<int?>(r, "UserId"),
                Priority = G.G<string>(r, "Priority") ?? "Medium",
                Category = G.G<string>(r, "Category") ?? "General",
                DueDate = G.G<DateTime>(r, "DueDate"),
                CreatedAt = G.G<DateTime>(r, "CreatedAt"),
                CompletedAt = G.G<DateTime?>(r, "CompletedAt"),
                IsCompleted = G.G<bool>(r, "IsCompleted"),
                Status = G.G<string>(r, "Status") ?? "Pending",
                StudentName = r.StudentName(db, "StudentName"),
                AdmissionNo = G.G<string>(r, "AdmissionNo"),
                CreatedByName = G.G<string>(r, "CreatedByName")
            });
    }

    public List<TaskItem> GetUserTasks(int userId, string status = "")
    {
        string where = $"WHERE t.UserId={userId}";
        if (!string.IsNullOrEmpty(status))
            where += $" AND t.Status='{status}'";

        return db.Sql($@"
            SELECT t.*, ISNULL(s.FullName,'') as StudentName, ISNULL(s.AdmissionNo,'') as AdmissionNo, ISNULL(u.FullName,'') as CreatedByName
            FROM Tasks t
            LEFT JOIN Students s ON s.StudentId=t.StudentId
            LEFT JOIN Users u ON u.UserId=t.UserId
            {where}
            ORDER BY t.DueDate ASC",
            r => new TaskItem {
                TaskId = G.G<int>(r, "TaskId"),
                Title = G.G<string>(r, "Title") ?? "",
                Description = G.G<string>(r, "Description"),
                StudentId = G.G<int?>(r, "StudentId"),
                UserId = G.G<int?>(r, "UserId"),
                Priority = G.G<string>(r, "Priority") ?? "Medium",
                Category = G.G<string>(r, "Category") ?? "General",
                DueDate = G.G<DateTime>(r, "DueDate"),
                CreatedAt = G.G<DateTime>(r, "CreatedAt"),
                CompletedAt = G.G<DateTime?>(r, "CompletedAt"),
                IsCompleted = G.G<bool>(r, "IsCompleted"),
                Status = G.G<string>(r, "Status") ?? "Pending",
                StudentName = r.StudentName(db, "StudentName"),
                AdmissionNo = G.G<string>(r, "AdmissionNo"),
                CreatedByName = G.G<string>(r, "CreatedByName")
            });
    }

    public List<TaskItem> GetPendingTasks(int userId)
    {
        return db.Sql("EXEC sp_GetPendingTasks @UserId=" + userId,
            r => new TaskItem {
                TaskId = G.G<int>(r, "TaskId"),
                Title = G.G<string>(r, "Title") ?? "",
                Description = G.G<string>(r, "Description"),
                StudentId = G.G<int?>(r, "StudentId"),
                UserId = G.G<int?>(r, "UserId"),
                Priority = G.G<string>(r, "Priority") ?? "Medium",
                Category = G.G<string>(r, "Category") ?? "General",
                DueDate = G.G<DateTime>(r, "DueDate"),
                CreatedAt = G.G<DateTime>(r, "CreatedAt"),
                CompletedAt = G.G<DateTime?>(r, "CompletedAt"),
                IsCompleted = G.G<bool>(r, "IsCompleted"),
                Status = G.G<string>(r, "Status") ?? "Pending"
            });
    }

    public TaskItem? GetTaskById(int taskId)
    {
        var tasks = db.Sql($@"
            SELECT t.*, ISNULL(s.FullName,'') as StudentName, ISNULL(s.AdmissionNo,'') as AdmissionNo, ISNULL(u.FullName,'') as CreatedByName
            FROM Tasks t
            LEFT JOIN Students s ON s.StudentId=t.StudentId
            LEFT JOIN Users u ON u.UserId=t.UserId
            WHERE t.TaskId={taskId}",
            r => new TaskItem {
                TaskId = G.G<int>(r, "TaskId"),
                Title = G.G<string>(r, "Title") ?? "",
                Description = G.G<string>(r, "Description"),
                StudentId = G.G<int?>(r, "StudentId"),
                UserId = G.G<int?>(r, "UserId"),
                Priority = G.G<string>(r, "Priority") ?? "Medium",
                Category = G.G<string>(r, "Category") ?? "General",
                DueDate = G.G<DateTime>(r, "DueDate"),
                CreatedAt = G.G<DateTime>(r, "CreatedAt"),
                CompletedAt = G.G<DateTime?>(r, "CompletedAt"),
                IsCompleted = G.G<bool>(r, "IsCompleted"),
                Status = G.G<string>(r, "Status") ?? "Pending",
                StudentName = r.StudentName(db, "StudentName"),
                AdmissionNo = G.G<string>(r, "AdmissionNo"),
                CreatedByName = G.G<string>(r, "CreatedByName")
            });
        return tasks.FirstOrDefault();
    }

    public void SaveTask(TaskItem task)
    {
        db.Exec("sp_SaveTask", new() {
            { "@TaskId", task.TaskId },
            { "@Title", task.Title },
            { "@Description", task.Description },
            { "@StudentId", task.StudentId },
            { "@UserId", task.UserId },
            { "@Priority", task.Priority },
            { "@Category", task.Category },
            { "@DueDate", task.DueDate },
            { "@Status", task.Status },
            { "@IsCompleted", task.IsCompleted }
        });
    }

    public void CompleteTask(int taskId)
    {
        db.Exec("sp_CompleteTask", new() { { "@TaskId", taskId } });
    }

    public void DeleteTask(int taskId)
    {
        db.Exec("sp_DeleteTask", new() { { "@TaskId", taskId } });
    }

    public int GetPendingTaskCount(int userId)
    {
        var result = db.Sql($"SELECT COUNT(*) as cnt FROM Tasks WHERE UserId={userId} AND IsCompleted=0",
            r => G.G<int>(r, "cnt"));
        return result.FirstOrDefault();
    }
}
