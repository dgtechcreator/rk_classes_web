IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Notifications' AND xtype='U')
BEGIN
    CREATE TABLE Notifications (
        NotificationId INT PRIMARY KEY IDENTITY(1,1),
        UserId INT NOT NULL,
        TaskId INT,
        Title NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Type] NVARCHAR(50) DEFAULT 'Task', -- Task, FeesDue, Attendance, etc.
        [Status] NVARCHAR(20) DEFAULT 'Unread', -- Unread, Read, Dismissed
        CreatedAt DATETIME DEFAULT GETDATE(),
        ReadAt DATETIME,
        [Action] NVARCHAR(500), -- URL or action
        ReferenceId INT, -- StudentId, PaymentId, etc.
        FOREIGN KEY (UserId) REFERENCES Users(UserId),
        FOREIGN KEY (TaskId) REFERENCES Tasks(TaskId)
    );

    CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
    CREATE INDEX IX_Notifications_Status ON Notifications([Status]);
    CREATE INDEX IX_Notifications_CreatedAt ON Notifications(CreatedAt DESC);
END
