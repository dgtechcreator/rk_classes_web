-- ============================================================
-- 06_SubjectMaster.sql
-- Subject CRUD stored procedures
-- ============================================================
USE SchoolManagementDB;
GO

-- Get all subjects with class name and usage count
IF OBJECT_ID('sp_GetSubjects','P') IS NOT NULL DROP PROC sp_GetSubjects;
GO
CREATE PROCEDURE sp_GetSubjects
AS BEGIN
    SELECT s.SubjectId, s.SubjectName, s.SubjectCode, s.ClassId, s.MaxMarks, s.PassMarks, s.IsActive,
           c.ClassName,
           (SELECT COUNT(*) FROM TestMarks tm WHERE tm.SubjectId = s.SubjectId) AS UsageCount
    FROM   Subjects s
    LEFT JOIN Classes c ON c.ClassId = s.ClassId
    ORDER BY c.OrderNo, s.SubjectName;
END
GO

-- Save (insert or update) a subject
IF OBJECT_ID('sp_SaveSubject','P') IS NOT NULL DROP PROC sp_SaveSubject;
GO
CREATE PROCEDURE sp_SaveSubject
    @SubjectId   INT,
    @SubjectName NVARCHAR(200),
    @SubjectCode NVARCHAR(50) = NULL,
    @ClassId     INT,
    @MaxMarks    INT = 100,
    @PassMarks   INT = 35,
    @IsActive    BIT = 1,
    @NewId       INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    IF @SubjectId = 0
    BEGIN
        INSERT INTO Subjects(SubjectName, SubjectCode, ClassId, MaxMarks, PassMarks, IsActive)
        VALUES(@SubjectName, @SubjectCode, @ClassId, @MaxMarks, @PassMarks, @IsActive);
        SET @NewId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE Subjects
        SET SubjectName=@SubjectName, SubjectCode=@SubjectCode, ClassId=@ClassId,
            MaxMarks=@MaxMarks, PassMarks=@PassMarks, IsActive=@IsActive
        WHERE SubjectId=@SubjectId;
        SET @NewId = @SubjectId;
    END
END
GO

-- Soft-delete a subject (only if not used in any marks)
IF OBJECT_ID('sp_DeleteSubject','P') IS NOT NULL DROP PROC sp_DeleteSubject;
GO
CREATE PROCEDURE sp_DeleteSubject
    @SubjectId INT
AS BEGIN
    IF EXISTS(SELECT 1 FROM TestMarks WHERE SubjectId=@SubjectId)
        RAISERROR('Cannot delete: subject has marks recorded against it.',16,1);
    ELSE
        UPDATE Subjects SET IsActive=0 WHERE SubjectId=@SubjectId;
END
GO

PRINT '✅ Subject master SPs created.';
GO
