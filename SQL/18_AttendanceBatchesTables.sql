-- Create AttendanceBatches table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AttendanceBatches]') AND type in (N'U'))
BEGIN
    CREATE TABLE AttendanceBatches (
        BatchId INT PRIMARY KEY IDENTITY(1,1),
        BatchName NVARCHAR(255) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        IsActive BIT DEFAULT 1
    );
END

-- Create AttendanceBatchStudents table (junction table)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AttendanceBatchStudents]') AND type in (N'U'))
BEGIN
    CREATE TABLE AttendanceBatchStudents (
        BatchStudentId INT PRIMARY KEY IDENTITY(1,1),
        BatchId INT NOT NULL,
        StudentId INT NOT NULL,
        FOREIGN KEY (BatchId) REFERENCES AttendanceBatches(BatchId) ON DELETE CASCADE,
        FOREIGN KEY (StudentId) REFERENCES Students(StudentId) ON DELETE CASCADE
    );
END
