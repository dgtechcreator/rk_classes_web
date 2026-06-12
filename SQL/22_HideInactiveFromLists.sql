-- ============================================================
-- 22_HideInactiveFromLists.sql
-- Fix: Inactive (deleted) students and faculty should NOT
-- appear in the main list unless specifically filtered.
-- Run in SSMS: open file → F5
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Fix sp_GetStudents ────────────────────────────────────
IF OBJECT_ID('sp_GetStudents','P') IS NOT NULL DROP PROC sp_GetStudents;
GO
CREATE PROCEDURE sp_GetStudents
  @PageNo         INT           = 1,
  @PageSize       INT           = 15,
  @Search         NVARCHAR(200) = NULL,
  @ClassId        INT           = NULL,
  @SectionId      INT           = NULL,
  @BatchId        INT           = NULL,
  @AcademicYearId INT           = NULL,
  @Status         NVARCHAR(20)  = NULL,
  @TotalCount     INT           OUTPUT
AS BEGIN
  SET NOCOUNT ON;

  -- When no status filter: exclude Inactive (soft-deleted)
  -- When status filter set: show exactly that status
  SELECT @TotalCount = COUNT(*) FROM Students s
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%'
                         OR s.AdmissionNo LIKE '%'+@Search+'%'
                         OR s.FatherName  LIKE '%'+@Search+'%')
    AND (@ClassId        IS NULL OR s.ClassId        = @ClassId)
    AND (@SectionId      IS NULL OR s.SectionId      = @SectionId)
    AND (@BatchId        IS NULL OR s.BatchId        = @BatchId)
    AND (@AcademicYearId IS NULL OR s.AcademicYearId = @AcademicYearId)
    AND (
          (@Status IS NULL AND s.Status != 'Inactive')
          OR s.Status = @Status
        );

  SELECT s.*, c.ClassName, sec.SectionName, b.BatchName, ay.YearName
  FROM Students s
  LEFT JOIN Classes      c   ON c.ClassId      = s.ClassId
  LEFT JOIN Sections     sec ON sec.SectionId  = s.SectionId
  LEFT JOIN Batches      b   ON b.BatchId      = s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId      = s.AcademicYearId
  WHERE (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%'
                         OR s.AdmissionNo LIKE '%'+@Search+'%'
                         OR s.FatherName  LIKE '%'+@Search+'%')
    AND (@ClassId        IS NULL OR s.ClassId        = @ClassId)
    AND (@SectionId      IS NULL OR s.SectionId      = @SectionId)
    AND (@BatchId        IS NULL OR s.BatchId        = @BatchId)
    AND (@AcademicYearId IS NULL OR s.AcademicYearId = @AcademicYearId)
    AND (
          (@Status IS NULL AND s.Status != 'Inactive')
          OR s.Status = @Status
        )
  ORDER BY s.FullName
  OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ── Fix sp_GetFaculty ─────────────────────────────────────
IF OBJECT_ID('sp_GetFaculty','P') IS NOT NULL DROP PROC sp_GetFaculty;
GO
CREATE PROCEDURE sp_GetFaculty
    @Search        NVARCHAR(200) = NULL,
    @Status        NVARCHAR(20)  = NULL,
    @DesignationId INT           = NULL,
    @PageNo        INT           = 1,
    @PageSize      INT           = 15,
    @TotalCount    INT           OUTPUT
AS BEGIN
    SET NOCOUNT ON;

    SELECT @TotalCount = COUNT(*) FROM Faculty f
    WHERE (@Search        IS NULL OR f.FullName     LIKE '%'+@Search+'%'
                                  OR f.EmployeeCode LIKE '%'+@Search+'%'
                                  OR f.Phone        LIKE '%'+@Search+'%')
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId)
      AND (
            (@Status IS NULL AND f.Status != 'Inactive')
            OR f.Status = @Status
          );

    SELECT f.*, d.DesignationName
    FROM Faculty f
    LEFT JOIN Designations d ON d.DesignationId = f.DesignationId
    WHERE (@Search        IS NULL OR f.FullName     LIKE '%'+@Search+'%'
                                  OR f.EmployeeCode LIKE '%'+@Search+'%'
                                  OR f.Phone        LIKE '%'+@Search+'%')
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId)
      AND (
            (@Status IS NULL AND f.Status != 'Inactive')
            OR f.Status = @Status
          )
    ORDER BY f.FullName
    OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

PRINT 'Done. Inactive (deleted) records are now hidden from main lists.';
GO
