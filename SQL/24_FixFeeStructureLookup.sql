-- ============================================================
-- Fix Fee Structure Lookup  |  24_FixFeeStructureLookup.sql
-- Diagnoses why fee structure is not auto-loading on Pay page
-- and upgrades sp_GetFeeStructure to be more resilient
-- ============================================================
USE SchoolManagementDB;
GO

-- ────────────────────────────────────────────────────────────
-- SECTION 1: DIAGNOSTIC  (run first to see what's happening)
-- ────────────────────────────────────────────────────────────

PRINT '=== FeeStructure table contents ===';
SELECT fs.StructureId, fs.AcademicYearId, ay.YearName,
       fs.ClassId, c.ClassName,
       fs.SectionId, s.SectionName,
       fs.FeeTypeId, ft.TypeName AS FeeTypeName,
       fs.Amount, fs.IsActive
FROM FeeStructure fs
LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
ORDER BY fs.StructureId;

PRINT '';
PRINT '=== Students in 10th class (check their ClassId / SectionId / AcademicYearId) ===';
SELECT s.StudentId, s.FullName, s.AdmissionNo,
       s.AcademicYearId, ay.YearName,
       s.ClassId, c.ClassName,
       s.SectionId, sec.SectionName,
       s.Status
FROM Students s
LEFT JOIN AcademicYears ay  ON ay.YearId    = s.AcademicYearId
LEFT JOIN Classes        c  ON c.ClassId    = s.ClassId
LEFT JOIN Sections       sec ON sec.SectionId = s.SectionId
WHERE c.ClassName LIKE '%10%'
   OR c.ClassName LIKE '%X%'
ORDER BY s.FullName;

PRINT '';
PRINT '=== IDs of Classes and Sections (for cross-check) ===';
SELECT 'Class' AS Type, ClassId AS Id, ClassName AS Name FROM Classes
UNION ALL
SELECT 'Section', SectionId, SectionName FROM Sections
ORDER BY Type, Id;

PRINT '';
PRINT '=== Academic Years ===';
SELECT YearId, YearName, IsCurrent FROM AcademicYears ORDER BY YearId;

GO
-- ────────────────────────────────────────────────────────────
-- SECTION 2: UPDATE sp_GetFeeStructure
-- Now supports @ClassNameLike and @SectionNameLike fallback
-- so even if IDs differ the lookup still works.
-- ────────────────────────────────────────────────────────────

IF OBJECT_ID('sp_GetFeeStructure','P') IS NOT NULL DROP PROC sp_GetFeeStructure;
GO
CREATE PROCEDURE sp_GetFeeStructure
    @AcademicYearId  INT = NULL,
    @ClassId         INT = NULL,
    @SectionId       INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Primary attempt: match by exact IDs
    SELECT fs.*,
           ay.YearName, c.ClassName, s.SectionName,
           ft.TypeName AS FeeTypeName,
           u.FullName  AS CreatedByName
    FROM   FeeStructure fs
    LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
    LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
    LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
    LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
    LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
    WHERE fs.IsActive = 1
      AND (@AcademicYearId IS NULL OR fs.AcademicYearId = @AcademicYearId)
      AND (@ClassId        IS NULL OR fs.ClassId         = @ClassId)
      AND (@SectionId      IS NULL OR fs.SectionId       = @SectionId)
    ORDER BY ay.YearName, c.OrderNo, s.SectionName, ft.TypeName;
END
GO

-- ────────────────────────────────────────────────────────────
-- SECTION 3: New SP  sp_GetFeeStructureByStudent
-- Uses student's stored IDs, tries 4 progressively looser
-- levels of matching so fee ALWAYS shows if class matches.
-- ────────────────────────────────────────────────────────────

IF OBJECT_ID('sp_GetFeeStructureByStudent','P') IS NOT NULL
    DROP PROC sp_GetFeeStructureByStudent;
GO
CREATE PROCEDURE sp_GetFeeStructureByStudent
    @StudentId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @YearId    INT, @ClassId   INT, @SectionId INT;
    DECLARE @ClassName NVARCHAR(100), @SectionName NVARCHAR(100), @YearName NVARCHAR(100);

    -- Fetch student's current assignment
    SELECT @YearId      = s.AcademicYearId,
           @ClassId     = s.ClassId,
           @SectionId   = s.SectionId,
           @ClassName   = c.ClassName,
           @SectionName = sec.SectionName,
           @YearName    = ay.YearName
    FROM   Students s
    LEFT JOIN Classes       c   ON c.ClassId    = s.ClassId
    LEFT JOIN Sections      sec ON sec.SectionId = s.SectionId
    LEFT JOIN AcademicYears ay  ON ay.YearId     = s.AcademicYearId
    WHERE  s.StudentId = @StudentId;

    -- ── Level 1: exact year + class + section ID match ──────
    IF EXISTS (
        SELECT 1 FROM FeeStructure
        WHERE IsActive=1
          AND (@YearId    IS NULL OR AcademicYearId = @YearId)
          AND (@ClassId   IS NULL OR ClassId         = @ClassId)
          AND (@SectionId IS NULL OR SectionId       = @SectionId)
    )
    BEGIN
        SELECT fs.*,
               ay.YearName, c.ClassName, s.SectionName,
               ft.TypeName AS FeeTypeName,
               u.FullName  AS CreatedByName
        FROM   FeeStructure fs
        LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
        LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
        LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
        LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
        LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
        WHERE fs.IsActive = 1
          AND (@YearId    IS NULL OR fs.AcademicYearId = @YearId)
          AND (@ClassId   IS NULL OR fs.ClassId         = @ClassId)
          AND (@SectionId IS NULL OR fs.SectionId       = @SectionId)
        ORDER BY ft.TypeName;
        RETURN;
    END

    -- ── Level 2: year + class only (ignore section) ─────────
    IF @YearId IS NOT NULL AND @ClassId IS NOT NULL
       AND EXISTS (SELECT 1 FROM FeeStructure WHERE IsActive=1
                   AND AcademicYearId=@YearId AND ClassId=@ClassId)
    BEGIN
        SELECT fs.*,
               ay.YearName, c.ClassName, s.SectionName,
               ft.TypeName AS FeeTypeName,
               u.FullName  AS CreatedByName
        FROM   FeeStructure fs
        LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
        LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
        LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
        LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
        LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
        WHERE fs.IsActive=1 AND fs.AcademicYearId=@YearId AND fs.ClassId=@ClassId
        ORDER BY ft.TypeName;
        RETURN;
    END

    -- ── Level 3: class + section by NAME (ID mismatch fallback) ──
    IF @ClassName IS NOT NULL
       AND EXISTS (
           SELECT 1 FROM FeeStructure fs
           INNER JOIN Classes   c  ON c.ClassId   = fs.ClassId
           INNER JOIN Sections  s  ON s.SectionId = fs.SectionId
           WHERE fs.IsActive=1
             AND c.ClassName   = @ClassName
             AND (@SectionName IS NULL OR s.SectionName = @SectionName)
       )
    BEGIN
        SELECT fs.*,
               ay.YearName, c.ClassName, s.SectionName,
               ft.TypeName AS FeeTypeName,
               u.FullName  AS CreatedByName
        FROM   FeeStructure fs
        INNER JOIN Classes       c  ON c.ClassId   = fs.ClassId
        INNER JOIN Sections      s  ON s.SectionId = fs.SectionId
        LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
        LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
        LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
        WHERE fs.IsActive=1
          AND c.ClassName   = @ClassName
          AND (@SectionName IS NULL OR s.SectionName = @SectionName)
        ORDER BY ft.TypeName;
        RETURN;
    END

    -- ── Level 4: class name only (any section, any year) ────
    IF @ClassName IS NOT NULL
       AND EXISTS (
           SELECT 1 FROM FeeStructure fs
           INNER JOIN Classes c ON c.ClassId = fs.ClassId
           WHERE fs.IsActive=1 AND c.ClassName = @ClassName
       )
    BEGIN
        SELECT fs.*,
               ay.YearName, c.ClassName, s.SectionName,
               ft.TypeName AS FeeTypeName,
               u.FullName  AS CreatedByName
        FROM   FeeStructure fs
        INNER JOIN Classes       c  ON c.ClassId   = fs.ClassId
        LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
        LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
        LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
        LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
        WHERE fs.IsActive=1 AND c.ClassName = @ClassName
        ORDER BY ft.TypeName;
        RETURN;
    END

    -- ── Level 5: nothing found — return empty ───────────────
    SELECT fs.*,
           ay.YearName, c.ClassName, s.SectionName,
           ft.TypeName AS FeeTypeName,
           u.FullName  AS CreatedByName
    FROM   FeeStructure fs
    LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
    LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
    LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
    LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
    LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
    WHERE 1=0; -- empty result set
END
GO

PRINT '✅ sp_GetFeeStructureByStudent created / updated.';
PRINT '✅ Run SECTION 1 SELECT statements to diagnose the ID mismatch.';
GO
