-- ============================================================
-- Masters Module  |  04_Masters.sql
-- Academic Year, Class, Section, Batch
-- ============================================================
USE SchoolManagementDB;
GO

-- ── sp_GetMasters ─────────────────────────────────────────────
IF OBJECT_ID('sp_GetAcademicYears','P') IS NOT NULL DROP PROC sp_GetAcademicYears; GO
CREATE PROCEDURE sp_GetAcademicYears AS BEGIN
    SELECT *, (SELECT COUNT(*) FROM Students WHERE AcademicYearId=AcademicYears.YearId) AS StudentCount
    FROM AcademicYears WHERE IsActive=1 ORDER BY YearId DESC;
END
GO

IF OBJECT_ID('sp_SaveAcademicYear','P') IS NOT NULL DROP PROC sp_SaveAcademicYear; GO
CREATE PROCEDURE sp_SaveAcademicYear
    @YearId   INT = 0,
    @YearName NVARCHAR(20),
    @IsCurrent BIT = 0,
    @IsActive  BIT = 1,
    @NewId    INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    -- If setting current, clear other current flags
    IF @IsCurrent = 1
        UPDATE AcademicYears SET IsCurrent = 0 WHERE YearId <> @YearId;
    IF @YearId = 0 BEGIN
        INSERT INTO AcademicYears(YearName, IsCurrent, IsActive)
        VALUES(@YearName, @IsCurrent, @IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE AcademicYears
        SET YearName=@YearName, IsCurrent=@IsCurrent, IsActive=@IsActive
        WHERE YearId=@YearId;
        SET @NewId = @YearId;
    END
END
GO

IF OBJECT_ID('sp_DeleteAcademicYear','P') IS NOT NULL DROP PROC sp_DeleteAcademicYear; GO
CREATE PROCEDURE sp_DeleteAcademicYear @YearId INT AS BEGIN
    -- Soft delete only if no students linked
    IF NOT EXISTS(SELECT 1 FROM Students WHERE AcademicYearId=@YearId)
        UPDATE AcademicYears SET IsActive=0 WHERE YearId=@YearId;
    ELSE
        RAISERROR('Cannot delete: students are linked to this academic year.',16,1);
END
GO

-- ── Classes ───────────────────────────────────────────────────
IF OBJECT_ID('sp_GetClasses','P') IS NOT NULL DROP PROC sp_GetClasses; GO
CREATE PROCEDURE sp_GetClasses AS BEGIN
    SELECT *, (SELECT COUNT(*) FROM Students WHERE ClassId=Classes.ClassId AND Status='Active') AS StudentCount
    FROM Classes ORDER BY OrderNo;
END
GO

IF OBJECT_ID('sp_SaveClass','P') IS NOT NULL DROP PROC sp_SaveClass; GO
CREATE PROCEDURE sp_SaveClass
    @ClassId   INT = 0,
    @ClassName NVARCHAR(50),
    @OrderNo   INT = 0,
    @IsActive  BIT = 1,
    @NewId     INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @ClassId = 0 BEGIN
        INSERT INTO Classes(ClassName, OrderNo, IsActive) VALUES(@ClassName, @OrderNo, @IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE Classes SET ClassName=@ClassName, OrderNo=@OrderNo, IsActive=@IsActive WHERE ClassId=@ClassId;
        SET @NewId = @ClassId;
    END
END
GO

IF OBJECT_ID('sp_DeleteClass','P') IS NOT NULL DROP PROC sp_DeleteClass; GO
CREATE PROCEDURE sp_DeleteClass @ClassId INT AS BEGIN
    IF NOT EXISTS(SELECT 1 FROM Students WHERE ClassId=@ClassId AND Status='Active')
        UPDATE Classes SET IsActive=0 WHERE ClassId=@ClassId;
    ELSE
        RAISERROR('Cannot delete: active students are linked to this class.',16,1);
END
GO

-- ── Sections ──────────────────────────────────────────────────
IF OBJECT_ID('sp_GetSections','P') IS NOT NULL DROP PROC sp_GetSections; GO
CREATE PROCEDURE sp_GetSections AS BEGIN
    SELECT *, (SELECT COUNT(*) FROM Students WHERE SectionId=Sections.SectionId AND Status='Active') AS StudentCount
    FROM Sections ORDER BY SectionId;
END
GO

IF OBJECT_ID('sp_SaveSection','P') IS NOT NULL DROP PROC sp_SaveSection; GO
CREATE PROCEDURE sp_SaveSection
    @SectionId   INT = 0,
    @SectionName NVARCHAR(50),
    @IsActive    BIT = 1,
    @NewId       INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @SectionId = 0 BEGIN
        INSERT INTO Sections(SectionName, IsActive) VALUES(@SectionName, @IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE Sections SET SectionName=@SectionName, IsActive=@IsActive WHERE SectionId=@SectionId;
        SET @NewId = @SectionId;
    END
END
GO

IF OBJECT_ID('sp_DeleteSection','P') IS NOT NULL DROP PROC sp_DeleteSection; GO
CREATE PROCEDURE sp_DeleteSection @SectionId INT AS BEGIN
    IF NOT EXISTS(SELECT 1 FROM Students WHERE SectionId=@SectionId AND Status='Active')
        UPDATE Sections SET IsActive=0 WHERE SectionId=@SectionId;
    ELSE
        RAISERROR('Cannot delete: active students are linked to this section.',16,1);
END
GO

-- ── Batches ───────────────────────────────────────────────────
IF OBJECT_ID('sp_GetBatches','P') IS NOT NULL DROP PROC sp_GetBatches; GO
CREATE PROCEDURE sp_GetBatches AS BEGIN
    SELECT *, (SELECT COUNT(*) FROM Students WHERE BatchId=Batches.BatchId AND Status='Active') AS StudentCount
    FROM Batches ORDER BY BatchId;
END
GO

IF OBJECT_ID('sp_SaveBatch','P') IS NOT NULL DROP PROC sp_SaveBatch; GO
CREATE PROCEDURE sp_SaveBatch
    @BatchId   INT = 0,
    @BatchName NVARCHAR(50),
    @IsActive  BIT = 1,
    @NewId     INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @BatchId = 0 BEGIN
        INSERT INTO Batches(BatchName, IsActive) VALUES(@BatchName, @IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END ELSE BEGIN
        UPDATE Batches SET BatchName=@BatchName, IsActive=@IsActive WHERE BatchId=@BatchId;
        SET @NewId = @BatchId;
    END
END
GO

IF OBJECT_ID('sp_DeleteBatch','P') IS NOT NULL DROP PROC sp_DeleteBatch; GO
CREATE PROCEDURE sp_DeleteBatch @BatchId INT AS BEGIN
    IF NOT EXISTS(SELECT 1 FROM Students WHERE BatchId=@BatchId AND Status='Active')
        UPDATE Batches SET IsActive=0 WHERE BatchId=@BatchId;
    ELSE
        RAISERROR('Cannot delete: active students are linked to this batch.',16,1);
END
GO

PRINT '✅ Masters stored procedures created.';
GO
