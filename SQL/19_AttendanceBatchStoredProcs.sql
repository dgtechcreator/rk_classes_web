USE SchoolManagementDB;
GO

-- Get all attendance batches
IF OBJECT_ID('sp_GetAttendanceBatches', 'P') IS NOT NULL
    DROP PROCEDURE sp_GetAttendanceBatches;
GO

CREATE PROCEDURE sp_GetAttendanceBatches
AS
BEGIN
    SELECT BatchId, BatchName, CreatedAt, IsActive
    FROM AttendanceBatches
    WHERE IsActive = 1
    ORDER BY CreatedAt DESC;
END
GO

-- Get attendance batch by ID
IF OBJECT_ID('sp_GetAttendanceBatchById', 'P') IS NOT NULL
    DROP PROCEDURE sp_GetAttendanceBatchById;
GO

CREATE PROCEDURE sp_GetAttendanceBatchById
    @BatchId INT
AS
BEGIN
    SELECT BatchId, BatchName, CreatedAt, IsActive
    FROM AttendanceBatches
    WHERE BatchId = @BatchId AND IsActive = 1;
END
GO

-- Get students in a batch
IF OBJECT_ID('sp_GetBatchStudents', 'P') IS NOT NULL
    DROP PROCEDURE sp_GetBatchStudents;
GO

CREATE PROCEDURE sp_GetBatchStudents
    @BatchId INT
AS
BEGIN
    SELECT StudentId
    FROM AttendanceBatchStudents
    WHERE BatchId = @BatchId;
END
GO

-- Insert attendance batch
IF OBJECT_ID('sp_InsertAttendanceBatch', 'P') IS NOT NULL
    DROP PROCEDURE sp_InsertAttendanceBatch;
GO

CREATE PROCEDURE sp_InsertAttendanceBatch
    @BatchName NVARCHAR(255)
AS
BEGIN
    INSERT INTO AttendanceBatches (BatchName, CreatedAt, IsActive)
    VALUES (@BatchName, GETDATE(), 1);

    SELECT CAST(SCOPE_IDENTITY() AS INT);
END
GO

-- Insert batch student
IF OBJECT_ID('sp_InsertBatchStudent', 'P') IS NOT NULL
    DROP PROCEDURE sp_InsertBatchStudent;
GO

CREATE PROCEDURE sp_InsertBatchStudent
    @BatchId INT,
    @StudentId INT
AS
BEGIN
    INSERT INTO AttendanceBatchStudents (BatchId, StudentId)
    VALUES (@BatchId, @StudentId);
END
GO

-- Update attendance batch
IF OBJECT_ID('sp_UpdateAttendanceBatch', 'P') IS NOT NULL
    DROP PROCEDURE sp_UpdateAttendanceBatch;
GO

CREATE PROCEDURE sp_UpdateAttendanceBatch
    @BatchId INT,
    @BatchName NVARCHAR(255)
AS
BEGIN
    UPDATE AttendanceBatches
    SET BatchName = @BatchName
    WHERE BatchId = @BatchId;
END
GO

-- Delete attendance batch
IF OBJECT_ID('sp_DeleteAttendanceBatch', 'P') IS NOT NULL
    DROP PROCEDURE sp_DeleteAttendanceBatch;
GO

CREATE PROCEDURE sp_DeleteAttendanceBatch
    @BatchId INT
AS
BEGIN
    UPDATE AttendanceBatches
    SET IsActive = 0
    WHERE BatchId = @BatchId;
END
GO

PRINT 'Attendance Batch stored procedures created successfully!';
