IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Tasks' AND xtype='U')
BEGIN
    CREATE TABLE Tasks (
        TaskId INT PRIMARY KEY IDENTITY(1,1),
        Title NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX),
        StudentId INT,
        UserId INT,
        Priority NVARCHAR(20) DEFAULT 'Medium', -- High, Medium, Low
        Category NVARCHAR(50) DEFAULT 'General', -- FeesDue, Attendance, Marks, General
        DueDate DATETIME NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        CompletedAt DATETIME,
        IsCompleted BIT DEFAULT 0,
        [Status] NVARCHAR(20) DEFAULT 'Pending', -- Pending, Overdue, Completed, Dismissed
        FOREIGN KEY (StudentId) REFERENCES Students(StudentId) ON DELETE SET NULL,
        FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE SET NULL
    );

    CREATE INDEX IX_Tasks_Status ON Tasks([Status]);
    CREATE INDEX IX_Tasks_DueDate ON Tasks(DueDate);
    CREATE INDEX IX_Tasks_StudentId ON Tasks(StudentId);
END
