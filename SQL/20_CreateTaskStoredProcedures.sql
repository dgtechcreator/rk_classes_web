-- Save/Update Task
IF EXISTS (SELECT * FROM sys.objects WHERE type='P' AND name='sp_SaveTask')
    DROP PROCEDURE sp_SaveTask;
GO

CREATE PROCEDURE sp_SaveTask
    @TaskId INT,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX),
    @StudentId INT,
    @UserId INT,
    @Priority NVARCHAR(20),
    @Category NVARCHAR(50),
    @DueDate DATETIME,
    @Status NVARCHAR(20),
    @IsCompleted BIT
AS
BEGIN
    IF @TaskId = 0 OR @TaskId IS NULL
    BEGIN
        INSERT INTO Tasks (Title, Description, StudentId, UserId, Priority, Category, DueDate, Status, IsCompleted, CreatedAt)
        VALUES (@Title, @Description, @StudentId, @UserId, @Priority, @Category, @DueDate, @Status, @IsCompleted, GETDATE())
    END
    ELSE
    BEGIN
        UPDATE Tasks
        SET Title = @Title, Description = @Description, StudentId = @StudentId, Priority = @Priority,
            Category = @Category, DueDate = @DueDate, Status = @Status, IsCompleted = @IsCompleted,
            CompletedAt = CASE WHEN @IsCompleted = 1 THEN GETDATE() ELSE NULL END
        WHERE TaskId = @TaskId
    END
END
GO

-- Get Pending Tasks by User
IF EXISTS (SELECT * FROM sys.objects WHERE type='P' AND name='sp_GetPendingTasks')
    DROP PROCEDURE sp_GetPendingTasks;
GO

CREATE PROCEDURE sp_GetPendingTasks
    @UserId INT
AS
BEGIN
    SELECT TaskId, Title, Description, StudentId, UserId, Priority, Category, DueDate,
           CreatedAt, CompletedAt, IsCompleted, Status
    FROM Tasks
    WHERE UserId = @UserId AND IsCompleted = 0
    ORDER BY DueDate ASC
END
GO

-- Complete Task
IF EXISTS (SELECT * FROM sys.objects WHERE type='P' AND name='sp_CompleteTask')
    DROP PROCEDURE sp_CompleteTask;
GO

CREATE PROCEDURE sp_CompleteTask
    @TaskId INT
AS
BEGIN
    UPDATE Tasks
    SET IsCompleted = 1, Status = 'Completed', CompletedAt = GETDATE()
    WHERE TaskId = @TaskId
END
GO

-- Delete Task
IF EXISTS (SELECT * FROM sys.objects WHERE type='P' AND name='sp_DeleteTask')
    DROP PROCEDURE sp_DeleteTask;
GO

CREATE PROCEDURE sp_DeleteTask
    @TaskId INT
AS
BEGIN
    DELETE FROM Tasks WHERE TaskId = @TaskId
END
GO

-- Create Notification SP
IF EXISTS (SELECT * FROM sys.objects WHERE type='P' AND name='sp_CreateNotification')
    DROP PROCEDURE sp_CreateNotification;
GO

CREATE PROCEDURE sp_CreateNotification
    @UserId INT,
    @TaskId INT,
    @Title NVARCHAR(200),
    @Message NVARCHAR(MAX),
    @Type NVARCHAR(50),
    @Status NVARCHAR(20),
    @Action NVARCHAR(500),
    @ReferenceId INT
AS
BEGIN
    INSERT INTO Notifications (UserId, TaskId, Title, Message, Type, Status, Action, ReferenceId, CreatedAt)
    VALUES (@UserId, @TaskId, @Title, @Message, @Type, @Status, @Action, @ReferenceId, GETDATE())
END
GO
