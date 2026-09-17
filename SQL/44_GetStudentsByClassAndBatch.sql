-- ============================================================
-- 44_GetStudentsByClassAndBatch.sql
-- Get students by class name and batch name
-- ============================================================
USE SchoolManagementDB;
GO

IF OBJECT_ID('sp_GetStudentsByClassAndBatch','P') IS NOT NULL DROP PROC sp_GetStudentsByClassAndBatch;
GO

CREATE PROCEDURE sp_GetStudentsByClassAndBatch
  @ClassName NVARCHAR(50),
  @BatchName NVARCHAR(50)
AS BEGIN
  SET NOCOUNT ON;

  SELECT s.*,
    c.ClassName,
    sec.SectionName,
    b.BatchName,
    ay.YearName,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId = s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
  LEFT JOIN Batches b ON b.BatchId = s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId = s.AcademicYearId
  LEFT JOIN Users uc ON uc.UserId = s.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = s.DeletedBy
  WHERE c.ClassName = @ClassName AND b.BatchName = @BatchName
  ORDER BY s.FullName;
END
GO

PRINT '✅ Stored procedure sp_GetStudentsByClassAndBatch created.';
GO
