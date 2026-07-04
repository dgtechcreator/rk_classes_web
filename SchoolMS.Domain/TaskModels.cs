namespace SchoolMS.Domain;

public class TaskItem
{
    public int TaskId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? StudentId { get; set; }
    public int? UserId { get; set; }
    public string Priority { get; set; } = "Medium"; // High, Medium, Low
    public string Category { get; set; } = "General"; // FeesDue, Attendance, Marks, General, etc.
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; } = false;
    public string Status { get; set; } = "Pending"; // Pending, Overdue, Completed, Dismissed
    public string? StudentName { get; set; }
    public string? AdmissionNo { get; set; }
    public string? CreatedByName { get; set; }
}

public class Notification
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public int? TaskId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = "Task"; // Task, FeesDue, Attendance, etc.
    public string Status { get; set; } = "Unread"; // Unread, Read, Dismissed
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReadAt { get; set; }
    public string? Action { get; set; } // URL or action to perform when clicked
    public int? ReferenceId { get; set; } // StudentId, PaymentId, etc.
}
