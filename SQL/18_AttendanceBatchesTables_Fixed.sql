USE SchoolManagementDB;
GO

-- Drop existing tables if they exist (in correct order to respect foreign keys)
IF OBJECT_ID('[dbo].[AttendanceBatchStudents]', 'U') IS NOT NULL
    DROP TABLE [dbo].[AttendanceBatchStudents];

IF OBJECT_ID('[dbo].[AttendanceBatches]', 'U') IS NOT NULL
    DROP TABLE [dbo].[AttendanceBatches];

GO

-- Create AttendanceBatches table
CREATE TABLE [dbo].[AttendanceBatches] (
    [BatchId] INT PRIMARY KEY IDENTITY(1,1),
    [BatchName] NVARCHAR(255) NOT NULL,
    [CreatedAt] DATETIME DEFAULT GETDATE(),
    [IsActive] BIT DEFAULT 1
);

GO

-- Create AttendanceBatchStudents table
CREATE TABLE [dbo].[AttendanceBatchStudents] (
    [BatchStudentId] INT PRIMARY KEY IDENTITY(1,1),
    [BatchId] INT NOT NULL,
    [StudentId] INT NOT NULL,
    CONSTRAINT [FK_AttendanceBatchStudents_AttendanceBatches] FOREIGN KEY ([BatchId])
        REFERENCES [dbo].[AttendanceBatches]([BatchId]) ON DELETE CASCADE,
    CONSTRAINT [FK_AttendanceBatchStudents_Students] FOREIGN KEY ([StudentId])
        REFERENCES [dbo].[Students]([StudentId]) ON DELETE CASCADE
);

GO

-- Create indexes for better performance
CREATE INDEX [IX_AttendanceBatchStudents_BatchId] ON [dbo].[AttendanceBatchStudents]([BatchId]);
CREATE INDEX [IX_AttendanceBatchStudents_StudentId] ON [dbo].[AttendanceBatchStudents]([StudentId]);

GO

PRINT 'AttendanceBatches tables created successfully!';
