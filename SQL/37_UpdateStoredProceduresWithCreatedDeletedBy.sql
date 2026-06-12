-- ============================================================
-- 37_UpdateStoredProceduresWithCreatedDeletedBy.sql
-- Update all stored procedures to include CreatedByName and DeletedByName
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Classes ──────────────────────────────────────────────────
IF OBJECT_ID('sp_GetClasses','P') IS NOT NULL DROP PROC sp_GetClasses; GO
CREATE PROCEDURE sp_GetClasses AS BEGIN
  SELECT c.*,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM Classes c
  LEFT JOIN Users uc ON uc.UserId = c.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = c.DeletedBy
  ORDER BY c.ClassName;
END
GO

-- ── Sections ─────────────────────────────────────────────────
IF OBJECT_ID('sp_GetSections','P') IS NOT NULL DROP PROC sp_GetSections; GO
CREATE PROCEDURE sp_GetSections AS BEGIN
  SELECT s.*,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM Sections s
  LEFT JOIN Users uc ON uc.UserId = s.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = s.DeletedBy
  ORDER BY s.SectionName;
END
GO

-- ── Batches ─────────────────────────────────────────────────
IF OBJECT_ID('sp_GetBatches','P') IS NOT NULL DROP PROC sp_GetBatches; GO
CREATE PROCEDURE sp_GetBatches AS BEGIN
  SELECT b.*,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM Batches b
  LEFT JOIN Users uc ON uc.UserId = b.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = b.DeletedBy
  ORDER BY b.BatchName;
END
GO

-- ── Subjects ────────────────────────────────────────────────
IF OBJECT_ID('sp_GetSubjects','P') IS NOT NULL DROP PROC sp_GetSubjects; GO
CREATE PROCEDURE sp_GetSubjects @ClassId INT = NULL AS BEGIN
  SELECT s.*,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM Subjects s
  LEFT JOIN Users uc ON uc.UserId = s.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = s.DeletedBy
  WHERE (@ClassId IS NULL OR s.ClassId = @ClassId)
  ORDER BY s.SubjectName;
END
GO

-- ── FeeTypes ────────────────────────────────────────────────
IF OBJECT_ID('sp_GetFeeTypes','P') IS NOT NULL DROP PROC sp_GetFeeTypes; GO
CREATE PROCEDURE sp_GetFeeTypes AS BEGIN
  SELECT ft.*,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName
  FROM FeeTypes ft
  LEFT JOIN Users uc ON uc.UserId = ft.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = ft.DeletedBy
  WHERE ft.IsActive = 1
  ORDER BY ft.FeeTypeName;
END
GO

-- ── Update sp_GetFaculty to include DeletedByName ───────────
IF OBJECT_ID('sp_GetFaculty','P') IS NOT NULL DROP PROC sp_GetFaculty; GO
CREATE PROCEDURE sp_GetFaculty
    @Search   NVARCHAR(200) = NULL,
    @Status   NVARCHAR(20)  = NULL,
    @DesignationId INT       = NULL,
    @PageNo   INT = 1,
    @PageSize INT = 15,
    @TotalCount INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    SELECT @TotalCount = COUNT(*) FROM Faculty f
    WHERE (@Search        IS NULL OR f.FullName LIKE '%'+@Search+'%' OR f.EmployeeCode LIKE '%'+@Search+'%' OR f.Phone LIKE '%'+@Search+'%')
      AND (@Status        IS NULL OR f.Status        = @Status)
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId);

    SELECT f.*, d.DesignationName,
      uc.FullName AS CreatedByName,
      ud.FullName AS DeletedByName
    FROM Faculty f
    LEFT JOIN Designations d ON d.DesignationId = f.DesignationId
    LEFT JOIN Users uc ON uc.UserId = f.CreatedBy
    LEFT JOIN Users ud ON ud.UserId = f.DeletedBy
    WHERE (@Search        IS NULL OR f.FullName LIKE '%'+@Search+'%' OR f.EmployeeCode LIKE '%'+@Search+'%' OR f.Phone LIKE '%'+@Search+'%')
      AND (@Status        IS NULL OR f.Status        = @Status)
      AND (@DesignationId IS NULL OR f.DesignationId = @DesignationId)
    ORDER BY f.FullName
    OFFSET (@PageNo-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- ── Update sp_GetFacultyById to include DeletedByName ───────
IF OBJECT_ID('sp_GetFacultyById','P') IS NOT NULL DROP PROC sp_GetFacultyById; GO
CREATE PROCEDURE sp_GetFacultyById @FacultyId INT AS BEGIN
    SELECT f.*, d.DesignationName,
      uc.FullName AS CreatedByName,
      ud.FullName AS DeletedByName
    FROM Faculty f
    LEFT JOIN Designations d ON d.DesignationId = f.DesignationId
    LEFT JOIN Users uc ON uc.UserId = f.CreatedBy
    LEFT JOIN Users ud ON ud.UserId = f.DeletedBy
    WHERE f.FacultyId = @FacultyId;

    SELECT fs.*, c.ClassName, sub.SubjectName, sec.SectionName
    FROM FacultySubjects fs
    LEFT JOIN Classes   c   ON c.ClassId   = fs.ClassId
    LEFT JOIN Subjects  sub ON sub.SubjectId= fs.SubjectId
    LEFT JOIN Sections  sec ON sec.SectionId= fs.SectionId
    WHERE fs.FacultyId=@FacultyId AND fs.IsActive=1;
END
GO

PRINT '✅ All stored procedures updated with CreatedByName and DeletedByName.';
GO
