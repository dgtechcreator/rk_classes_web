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

  -- Fee columns are required by FeesRepo.GetClassStudentFeeDetails (Finance > Class Detail drill-down
  -- and its CSV export) — without them every student silently shows ₹0 for fees/discount/collected/balance.
  SELECT s.*,
    c.ClassName,
    sec.SectionName,
    b.BatchName,
    ay.YearName,
    uc.FullName AS CreatedByName,
    ud.FullName AS DeletedByName,
    COALESCE(f.TotalFees, 0) AS TotalFees,
    COALESCE(f.Discount, 0) AS Discount,
    COALESCE(f.Collected, 0) AS Collected,
    COALESCE(f.TotalFees, 0) - COALESCE(f.Collected, 0) - COALESCE(f.Discount, 0) AS Balance
  FROM Students s
  LEFT JOIN Classes c ON c.ClassId = s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
  LEFT JOIN Batches b ON b.BatchId = s.BatchId
  LEFT JOIN AcademicYears ay ON ay.YearId = s.AcademicYearId
  LEFT JOIN Users uc ON uc.UserId = s.CreatedBy
  LEFT JOIN Users ud ON ud.UserId = s.DeletedBy
  LEFT JOIN (
    SELECT StudentId, SUM(Amount) AS TotalFees, SUM(PaidAmount) AS Collected, SUM(Discount) AS Discount
    FROM StudentFees
    GROUP BY StudentId
  ) f ON f.StudentId = s.StudentId
  WHERE c.ClassName = @ClassName AND b.BatchName = @BatchName
  ORDER BY s.FullName;
END
GO

PRINT '✅ Stored procedure sp_GetStudentsByClassAndBatch created.';
GO
