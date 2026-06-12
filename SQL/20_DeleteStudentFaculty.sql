-- ============================================================
-- 20_DeleteStudentFaculty.sql
-- Soft delete: sets Status = 'Inactive' — NO rows removed
-- Run this in SSMS: open file, press F5
-- ============================================================
USE SchoolManagementDB;
GO

-- ── sp_DeleteStudent  (soft delete) ───────────────────────
IF OBJECT_ID('sp_DeleteStudent','P') IS NOT NULL DROP PROC sp_DeleteStudent;
GO
CREATE PROCEDURE sp_DeleteStudent
    @StudentId INT
AS BEGIN
    SET NOCOUNT ON;
    UPDATE Students
    SET    Status = 'Inactive'
    WHERE  StudentId = @StudentId;
END
GO

-- ── sp_DeleteFaculty  (soft delete) ───────────────────────
IF OBJECT_ID('sp_DeleteFaculty','P') IS NOT NULL DROP PROC sp_DeleteFaculty;
GO
CREATE PROCEDURE sp_DeleteFaculty
    @FacultyId INT
AS BEGIN
    SET NOCOUNT ON;
    UPDATE Faculty
    SET    Status = 'Inactive'
    WHERE  FacultyId = @FacultyId;
END
GO

PRINT 'Done. sp_DeleteStudent and sp_DeleteFaculty created (soft delete - Status set to Inactive).';
GO
