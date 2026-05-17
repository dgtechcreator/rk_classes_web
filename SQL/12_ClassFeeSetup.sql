-- ============================================================
-- 12_ClassFeeSetup.sql
-- New table: fee amount per Batch + Class + Section (Medium/Stream)
-- ============================================================
USE SchoolManagementDB;
GO

IF NOT EXISTS(SELECT * FROM sys.tables WHERE name='ClassFeeSetup')
CREATE TABLE ClassFeeSetup (
    SetupId   INT IDENTITY PRIMARY KEY,
    BatchId   INT NOT NULL REFERENCES Batches(BatchId),
    ClassId   INT NOT NULL REFERENCES Classes(ClassId),
    SectionId INT NOT NULL REFERENCES Sections(SectionId),
    Amount    DECIMAL(10,2) NOT NULL,
    IsActive  BIT DEFAULT 1,
    CreatedAt DATETIME DEFAULT GETDATE(),
    CONSTRAINT UQ_ClassFeeSetup UNIQUE(BatchId, ClassId, SectionId)
);
GO

IF OBJECT_ID('sp_GetClassFeeSetup','P') IS NOT NULL DROP PROC sp_GetClassFeeSetup; GO
CREATE PROCEDURE sp_GetClassFeeSetup AS BEGIN
    SELECT fs.*, b.BatchName, c.ClassName, s.SectionName
    FROM ClassFeeSetup fs
    INNER JOIN Batches  b ON b.BatchId   = fs.BatchId
    INNER JOIN Classes  c ON c.ClassId   = fs.ClassId
    INNER JOIN Sections s ON s.SectionId = fs.SectionId
    ORDER BY c.OrderNo, b.BatchName, s.SectionName;
END
GO

IF OBJECT_ID('sp_SaveClassFeeSetup','P') IS NOT NULL DROP PROC sp_SaveClassFeeSetup; GO
CREATE PROCEDURE sp_SaveClassFeeSetup
    @SetupId  INT = 0,
    @BatchId  INT,
    @ClassId  INT,
    @SectionId INT,
    @Amount   DECIMAL(10,2),
    @NewId    INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @SetupId = 0 BEGIN
        IF EXISTS(SELECT 1 FROM ClassFeeSetup WHERE BatchId=@BatchId AND ClassId=@ClassId AND SectionId=@SectionId) BEGIN
            UPDATE ClassFeeSetup SET Amount=@Amount, IsActive=1
            WHERE BatchId=@BatchId AND ClassId=@ClassId AND SectionId=@SectionId;
            SET @NewId = (SELECT SetupId FROM ClassFeeSetup WHERE BatchId=@BatchId AND ClassId=@ClassId AND SectionId=@SectionId);
        END ELSE BEGIN
            INSERT INTO ClassFeeSetup(BatchId, ClassId, SectionId, Amount)
            VALUES(@BatchId, @ClassId, @SectionId, @Amount);
            SET @NewId = SCOPE_IDENTITY();
        END
    END ELSE BEGIN
        UPDATE ClassFeeSetup SET BatchId=@BatchId, ClassId=@ClassId, SectionId=@SectionId, Amount=@Amount
        WHERE SetupId=@SetupId;
        SET @NewId = @SetupId;
    END
END
GO

IF OBJECT_ID('sp_DeleteClassFeeSetup','P') IS NOT NULL DROP PROC sp_DeleteClassFeeSetup; GO
CREATE PROCEDURE sp_DeleteClassFeeSetup @SetupId INT AS BEGIN
    DELETE FROM ClassFeeSetup WHERE SetupId = @SetupId;
END
GO

IF OBJECT_ID('sp_GetFeeAmountBySelection','P') IS NOT NULL DROP PROC sp_GetFeeAmountBySelection; GO
CREATE PROCEDURE sp_GetFeeAmountBySelection
    @BatchId   INT,
    @ClassId   INT,
    @SectionId INT
AS BEGIN
    SELECT ISNULL(Amount, 0) AS Amount
    FROM ClassFeeSetup
    WHERE BatchId=@BatchId AND ClassId=@ClassId AND SectionId=@SectionId AND IsActive=1;
END
GO

PRINT '✅ ClassFeeSetup table and stored procedures created.';
GO
