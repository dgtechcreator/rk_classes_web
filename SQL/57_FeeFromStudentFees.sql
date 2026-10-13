-- ============================================================
-- 57_FeeFromStudentFees.sql   (run after 55 and 52)
-- One source of truth: a student's fee = his ACTIVE StudentFees rows.
--  * sp_GetFeeStructureByStudent (web Pay page, receipts, parent view, mobile API) now returns the student's active
--    StudentFees rows (with their own Amount) when he has any; only if he has none it falls back to the old lookup.
--    Finance / phone already sum StudentFees, so web and phone can no longer disagree.
--  * StudentFees.IsCustomAmount: a fee agreed for one student that differs from the fee head. Editing the head never
--    overwrites it.
--  * sp_SyncStudentFees: called after every student save - switches off unpaid rows of a section the student has left
--    and applies the heads of his current section (nothing is deleted).
-- ============================================================
IF COL_LENGTH('StudentFees','IsCustomAmount') IS NULL
    ALTER TABLE StudentFees ADD IsCustomAmount BIT NOT NULL CONSTRAINT DF_StudentFees_IsCustomAmount DEFAULT 0;
GO

-- ---- lookup: StudentFees first, old 4-level structure lookup as fallback --------------------------------------
DECLARE @def NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID('sp_GetFeeStructureByStudent'));
IF @def NOT LIKE '%StudentFees%'
BEGIN
    DECLARE @pos INT = CHARINDEX(N'Level 1: exact year', @def);
    IF @pos = 0 RAISERROR('marker not found in sp_GetFeeStructureByStudent', 16, 1);
    SET @pos = CHARINDEX(CHAR(10), @def, @pos) + 1;   -- start of the line after the Level 1 comment
    DECLARE @block NVARCHAR(MAX) = N'-- Level 0: the student''s own active StudentFees rows (single source of truth)
    IF EXISTS (SELECT 1 FROM StudentFees WHERE StudentId=@StudentId AND IsActive=1)
    BEGIN
        SELECT fs.StructureId, fs.AcademicYearId, fs.ClassId, fs.SectionId, fs.FeeTypeId,
               sf.Amount AS Amount, fs.DueDay, fs.IsMonthly, fs.Remarks, fs.IsActive,
               fs.CreatedBy, fs.CreatedAt, fs.DeletedBy, fs.DeletedAt,
               ay.YearName, c.ClassName, s.SectionName,
               ft.TypeName AS FeeTypeName,
               u.FullName  AS CreatedByName
        FROM   StudentFees sf
        INNER JOIN FeeStructure  fs ON fs.StructureId = sf.StructureId
        LEFT JOIN AcademicYears ay ON ay.YearId    = fs.AcademicYearId
        LEFT JOIN Classes        c  ON c.ClassId   = fs.ClassId
        LEFT JOIN Sections       s  ON s.SectionId = fs.SectionId
        LEFT JOIN FeeTypes       ft ON ft.FeeTypeId= fs.FeeTypeId
        LEFT JOIN Users          u  ON u.UserId    = fs.CreatedBy
        WHERE sf.StudentId = @StudentId AND sf.IsActive = 1
        ORDER BY ft.TypeName;
        RETURN;
    END

    ';
    SET @def = STUFF(@def, @pos, 0, @block);
    SET @def = STUFF(@def, CHARINDEX('CREATE PROCEDURE', @def), LEN('CREATE PROCEDURE'), 'ALTER PROCEDURE');
    EXEC sp_executesql @def;
END
GO

-- ---- applying heads to a student keeps custom amounts ---------------------------------------------------------
ALTER PROCEDURE [dbo].[sp_ApplyFeesToStudent] @StudentId INT AS BEGIN
    SET NOCOUNT ON;
    DECLARE @YearId INT, @ClassId INT, @SectionId INT;
    SELECT @YearId=AcademicYearId, @ClassId=ClassId, @SectionId=SectionId
    FROM Students WHERE StudentId=@StudentId;

    UPDATE sf SET IsActive=1, Amount=CASE WHEN sf.IsCustomAmount=1 THEN sf.Amount ELSE fs.Amount END,
                  DeactivatedAt=NULL, DeactivatedReason=NULL
    FROM StudentFees sf
    JOIN FeeStructure fs ON fs.StructureId=sf.StructureId
    WHERE sf.StudentId=@StudentId AND sf.IsActive=0 AND fs.IsActive=1
      AND fs.AcademicYearId=@YearId AND fs.ClassId=@ClassId AND fs.SectionId=@SectionId;

    INSERT INTO StudentFees(StudentId, StructureId, AcademicYearId, FeeTypeId, Amount, DueDate)
    SELECT @StudentId, fs.StructureId, fs.AcademicYearId, fs.FeeTypeId, fs.Amount,
           DATEADD(DAY, fs.DueDay-1, DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1))
    FROM FeeStructure fs
    WHERE fs.AcademicYearId = @YearId
      AND fs.ClassId = @ClassId
      AND fs.SectionId = @SectionId
      AND fs.IsActive = 1
      AND NOT EXISTS(
          SELECT 1 FROM StudentFees sf
          WHERE sf.StudentId=@StudentId AND sf.StructureId=fs.StructureId
      );
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_SyncStudentFees] @StudentId INT AS BEGIN
    SET NOCOUNT ON;
    DECLARE @YearId INT, @ClassId INT, @SectionId INT;
    SELECT @YearId=AcademicYearId, @ClassId=ClassId, @SectionId=SectionId FROM Students WHERE StudentId=@StudentId;

    -- Only when the (new) section has its own heads: switch off unpaid rows that belong to another section/class/year
    IF @SectionId IS NOT NULL AND EXISTS (SELECT 1 FROM FeeStructure WHERE IsActive=1
           AND AcademicYearId=@YearId AND ClassId=@ClassId AND SectionId=@SectionId)
    BEGIN
        UPDATE sf
        SET IsActive=0, DeactivatedAt=GETDATE(), DeactivatedReason='Student moved to another class/section'
        FROM StudentFees sf
        JOIN FeeStructure old ON old.StructureId=sf.StructureId
        WHERE sf.StudentId=@StudentId AND sf.IsActive=1 AND sf.Status='Pending' AND ISNULL(sf.PaidAmount,0)=0
          AND (old.AcademicYearId<>@YearId OR old.ClassId<>@ClassId OR old.SectionId<>@SectionId);
    END

    EXEC sp_ApplyFeesToStudent @StudentId;
END
GO

-- ---- editing a fee head must not overwrite a custom per-student amount ----------------------------------------
DECLARE @sv NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID('sp_SaveFeeStructure'));
IF @sv NOT LIKE '%IsCustomAmount%'
BEGIN
    SET @sv = REPLACE(@sv, N'WHERE StructureId=@StructureId AND Status=''Pending'' AND IsActive=1;',
                           N'WHERE StructureId=@StructureId AND Status=''Pending'' AND IsActive=1 AND IsCustomAmount=0;');
    SET @sv = REPLACE(@sv, N'UPDATE sf SET IsActive=1, Amount=@Amount, DeactivatedAt=NULL, DeactivatedReason=NULL',
                           N'UPDATE sf SET IsActive=1, Amount=CASE WHEN sf.IsCustomAmount=1 THEN sf.Amount ELSE @Amount END, DeactivatedAt=NULL, DeactivatedReason=NULL');
    IF @sv NOT LIKE '%IsCustomAmount%' RAISERROR('sp_SaveFeeStructure patch did not apply (run 52 first)', 16, 1);
    SET @sv = STUFF(@sv, CHARINDEX('CREATE PROCEDURE', @sv), LEN('CREATE PROCEDURE'), 'ALTER PROCEDURE');
    EXEC sp_executesql @sv;
END
GO

-- ---- DATA: the three students whose fee the user confirmed (13-Oct-2026) --------------------------------------
-- Raunak 331 (10th English) = 22000, Raj 86 (9th English) = 17000, Rudra 197 (9th English) = 15000.
-- Their old wrong row is switched off (not deleted) and the row of their own head is added with the agreed amount.
DECLARE @fix TABLE (StudentId INT, Amount DECIMAL(10,2));
INSERT @fix VALUES (331, 22000), (86, 17000), (197, 15000);

UPDATE sf
SET IsActive=0, DeactivatedAt=GETDATE(), DeactivatedReason='Fee confirmed by admin; row belonged to another class/section'
FROM StudentFees sf
JOIN @fix f ON f.StudentId=sf.StudentId
JOIN Students s ON s.StudentId=sf.StudentId
JOIN FeeStructure old ON old.StructureId=sf.StructureId
WHERE sf.IsActive=1 AND (old.ClassId<>s.ClassId OR old.SectionId<>s.SectionId OR old.AcademicYearId<>s.AcademicYearId);
PRINT 'old rows switched off: ' + CAST(@@ROWCOUNT AS VARCHAR);

INSERT INTO StudentFees(StudentId, StructureId, AcademicYearId, FeeTypeId, Amount, DueDate, IsCustomAmount)
SELECT s.StudentId, own.StructureId, own.AcademicYearId, own.FeeTypeId, f.Amount,
       DATEADD(DAY, own.DueDay-1, DATEFROMPARTS(YEAR(GETDATE()),MONTH(GETDATE()),1)),
       CASE WHEN f.Amount<>own.Amount THEN 1 ELSE 0 END
FROM @fix f
JOIN Students s ON s.StudentId=f.StudentId
JOIN FeeStructure own ON own.IsActive=1 AND own.AcademicYearId=s.AcademicYearId AND own.ClassId=s.ClassId AND own.SectionId=s.SectionId
WHERE NOT EXISTS (SELECT 1 FROM StudentFees x WHERE x.StudentId=s.StudentId AND x.StructureId=own.StructureId AND x.IsActive=1);
PRINT 'new rows added: ' + CAST(@@ROWCOUNT AS VARCHAR);
GO
