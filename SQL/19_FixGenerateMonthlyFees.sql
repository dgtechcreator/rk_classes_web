-- ============================================================
-- 19_FixGenerateMonthlyFees.sql
-- Fix: "sp_GenerateMonthlyFees has too many arguments"
-- Change return from SELECT @@ROWCOUNT to OUTPUT parameter
-- ============================================================
USE SchoolManagementDB;
GO

IF OBJECT_ID('sp_GenerateMonthlyFees','P') IS NOT NULL DROP PROC sp_GenerateMonthlyFees;
GO
CREATE PROCEDURE sp_GenerateMonthlyFees
    @Month          NVARCHAR(20),
    @AcademicYearId INT,
    @FeesGenerated  INT OUTPUT
AS BEGIN
    SET NOCOUNT ON;
    DECLARE @DueBase DATE = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

    INSERT INTO StudentFees(StudentId, StructureId, AcademicYearId, FeeTypeId, Amount, Month, DueDate)
    SELECT s.StudentId, fs.StructureId, fs.AcademicYearId, fs.FeeTypeId, fs.Amount,
           @Month, DATEADD(DAY, fs.DueDay - 1, @DueBase)
    FROM FeeStructure fs
    INNER JOIN Students s ON s.AcademicYearId = fs.AcademicYearId
                          AND s.ClassId        = fs.ClassId
                          AND s.SectionId      = fs.SectionId
    WHERE fs.IsActive  = 1
      AND fs.IsMonthly = 1
      AND fs.AcademicYearId = @AcademicYearId
      AND s.Status = 'Active'
      AND NOT EXISTS (
          SELECT 1 FROM StudentFees sf2
          WHERE sf2.StudentId   = s.StudentId
            AND sf2.StructureId = fs.StructureId
            AND sf2.Month       = @Month
      );

    SET @FeesGenerated = @@ROWCOUNT;
END
GO

PRINT 'Done. sp_GenerateMonthlyFees now uses OUTPUT parameter @FeesGenerated.';
GO
