-- ============================================================
-- 21_RestoreStudentFaculty.sql
-- sp_RestoreStudent and sp_RestoreFaculty (set Status = Active)
-- Run in SSMS: open file → F5
-- ============================================================
USE SchoolManagementDB;
GO

IF OBJECT_ID('sp_RestoreStudent','P') IS NOT NULL DROP PROC sp_RestoreStudent;
GO
CREATE PROCEDURE sp_RestoreStudent
    @StudentId INT
AS BEGIN
    SET NOCOUNT ON;
    UPDATE Students SET Status = 'Active' WHERE StudentId = @StudentId;
END
GO

IF OBJECT_ID('sp_RestoreFaculty','P') IS NOT NULL DROP PROC sp_RestoreFaculty;
GO
CREATE PROCEDURE sp_RestoreFaculty
    @FacultyId INT
AS BEGIN
    SET NOCOUNT ON;
    UPDATE Faculty SET Status = 'Active' WHERE FacultyId = @FacultyId;
END
GO

PRINT 'Done. sp_RestoreStudent and sp_RestoreFaculty created.';
GO
