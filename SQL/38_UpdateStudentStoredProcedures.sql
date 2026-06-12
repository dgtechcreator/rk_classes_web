-- ============================================================
-- 38_UpdateStudentStoredProcedures.sql
-- Update Student stored procedures to include CreatedByName and DeletedByName
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Update sp_GetStudents to include CreatedByName and DeletedByName ────
IF OBJECT_ID('sp_GetStudents','P') IS NOT NULL DROP PROC sp_GetStudents; GO
CREATE PROCEDURE sp_GetStudents
  @PageNo INT=1, @PageSize INT=15, @Search NVARCHAR(200)=NULL,
  @ClassId INT=NULL, @SectionId INT=NULL, @BatchId INT=NULL,
  @AcademicYearId INT=NULL, @Status NVARCHAR(20)=NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;
  SELECT @TotalCount=COUNT(*) FROM Students s
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%' OR s.FatherName LIKE '%'+@Search+'%')
    AND (@ClassId IS NULL OR s.ClassId=@ClassId)
    AND (@SectionId IS NULL OR s.SectionId=@SectionId)
    AND (@BatchId IS NULL OR s.BatchId=@BatchId)
    AND (@AcademicYearId IS NULL OR s.AcademicYearId=@AcademicYearId)
    AND (@Status IS NULL OR s.Status=@Status);
  SELECT s.*,c.ClassName,sec.SectionName,b.BatchName,ay.YearName,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
  LEFT JOIN Batches b ON b.BatchId=s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId=s.AcademicYearId
  LEFT JOIN Users uc ON uc.UserId=s.CreatedBy
  LEFT JOIN Users ud ON ud.UserId=s.DeletedBy
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%' OR s.FatherName LIKE '%'+@Search+'%')
    AND (@ClassId IS NULL OR s.ClassId=@ClassId)
    AND (@SectionId IS NULL OR s.SectionId=@SectionId)
    AND (@BatchId IS NULL OR s.BatchId=@BatchId)
    AND (@AcademicYearId IS NULL OR s.AcademicYearId=@AcademicYearId)
    AND (@Status IS NULL OR s.Status=@Status)
  ORDER BY s.FullName
  OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ── Update sp_GetStudentById to include CreatedByName and DeletedByName ────
IF OBJECT_ID('sp_GetStudentById','P') IS NOT NULL DROP PROC sp_GetStudentById; GO
CREATE PROCEDURE sp_GetStudentById @StudentId INT AS BEGIN
  SELECT s.*,c.ClassName,sec.SectionName,b.BatchName,ay.YearName,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId=s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId=s.SectionId
  LEFT JOIN Batches b ON b.BatchId=s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId=s.AcademicYearId
  LEFT JOIN Users uc ON uc.UserId=s.CreatedBy
  LEFT JOIN Users ud ON ud.UserId=s.DeletedBy
  WHERE s.StudentId=@StudentId;
END
GO

PRINT '✅ Student stored procedures updated with CreatedByName and DeletedByName.';
GO
