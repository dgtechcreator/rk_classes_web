-- ============================================================
-- 30: Soft-delete for FeePayments
-- ============================================================

-- Add soft-delete columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FeePayments') AND name='IsDeleted')
    ALTER TABLE FeePayments ADD IsDeleted BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FeePayments') AND name='DeletedAt')
    ALTER TABLE FeePayments ADD DeletedAt DATETIME NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('FeePayments') AND name='DeletedBy')
    ALTER TABLE FeePayments ADD DeletedBy INT NULL;
GO

-- ── sp_DeleteFeePayment ────────────────────────────────────
IF OBJECT_ID('sp_DeleteFeePayment','P') IS NOT NULL DROP PROC sp_DeleteFeePayment;
GO
CREATE PROCEDURE sp_DeleteFeePayment
  @PaymentId INT,
  @DeletedBy INT = NULL
AS BEGIN
  SET NOCOUNT ON;
  UPDATE FeePayments
  SET IsDeleted = 1,
      DeletedAt = GETDATE(),
      DeletedBy = @DeletedBy
  WHERE PaymentId = @PaymentId;
END
GO

-- ── sp_RestoreFeePayment ───────────────────────────────────
IF OBJECT_ID('sp_RestoreFeePayment','P') IS NOT NULL DROP PROC sp_RestoreFeePayment;
GO
CREATE PROCEDURE sp_RestoreFeePayment
  @PaymentId INT
AS BEGIN
  SET NOCOUNT ON;
  UPDATE FeePayments
  SET IsDeleted = 0,
      DeletedAt = NULL,
      DeletedBy = NULL
  WHERE PaymentId = @PaymentId;
END
GO

-- ── sp_GetDeletedFeePayments ───────────────────────────────
IF OBJECT_ID('sp_GetDeletedFeePayments','P') IS NOT NULL DROP PROC sp_GetDeletedFeePayments;
GO
CREATE PROCEDURE sp_GetDeletedFeePayments
AS BEGIN
  SET NOCOUNT ON;
  SELECT fp.*, s.FullName AS StudentName, s.AdmissionNo,
         c.ClassName, sec.SectionName, b.BatchName,
         ft.TypeName AS FeeTypeName,
         u.FullName  AS CollectorName,
         du.FullName AS DeletedByName,
         NULL AS StudentPhone, NULL AS FatherPhone,
         NULL AS StudentEmail, NULL AS StudentAddress, NULL AS RollNo
  FROM FeePayments fp
  LEFT JOIN Students s   ON s.StudentId   = fp.StudentId
  LEFT JOIN Classes  c   ON c.ClassId     = s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
  LEFT JOIN Batches  b   ON b.BatchId     = s.BatchId
  LEFT JOIN FeeTypes ft  ON ft.FeeTypeId  = fp.FeeTypeId
  LEFT JOIN Users    u   ON u.UserId      = fp.CollectedBy
  LEFT JOIN Users    du  ON du.UserId     = fp.DeletedBy
  WHERE fp.IsDeleted = 1
  ORDER BY fp.DeletedAt DESC;
END
GO

-- ── Update sp_GetFeePayments to filter IsDeleted=0 ─────────
-- Re-create the SP adding WHERE IsDeleted=0 (safe to run multiple times)
IF OBJECT_ID('sp_GetFeePayments','P') IS NOT NULL DROP PROC sp_GetFeePayments;
GO
CREATE PROCEDURE sp_GetFeePayments
  @PageNo     INT           = 1,
  @PageSize   INT           = 15,
  @Search     NVARCHAR(100) = NULL,
  @Month      NVARCHAR(20)  = NULL,
  @FeeTypeId  INT           = NULL,
  @TotalCount INT OUTPUT
AS BEGIN
  SET NOCOUNT ON;

  SELECT @TotalCount = COUNT(*)
  FROM FeePayments fp
  LEFT JOIN Students s ON s.StudentId = fp.StudentId
  WHERE fp.IsDeleted = 0
    AND (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR fp.ReceiptNo LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%')
    AND (@Month IS NULL OR fp.Month = @Month)
    AND (@FeeTypeId IS NULL OR fp.FeeTypeId = @FeeTypeId);

  SELECT fp.*, s.FullName AS StudentName, s.AdmissionNo,
         c.ClassName, sec.SectionName, b.BatchName,
         ft.TypeName AS FeeTypeName, u.FullName AS CollectorName,
         NULL AS StudentPhone, NULL AS FatherPhone,
         NULL AS StudentEmail, NULL AS StudentAddress, NULL AS RollNo
  FROM FeePayments fp
  LEFT JOIN Students s   ON s.StudentId   = fp.StudentId
  LEFT JOIN Classes  c   ON c.ClassId     = s.ClassId
  LEFT JOIN Sections sec ON sec.SectionId = s.SectionId
  LEFT JOIN Batches  b   ON b.BatchId     = s.BatchId
  LEFT JOIN FeeTypes ft  ON ft.FeeTypeId  = fp.FeeTypeId
  LEFT JOIN Users    u   ON u.UserId      = fp.CollectedBy
  WHERE fp.IsDeleted = 0
    AND (@Search IS NULL OR s.FullName LIKE '%'+@Search+'%' OR fp.ReceiptNo LIKE '%'+@Search+'%' OR s.AdmissionNo LIKE '%'+@Search+'%')
    AND (@Month IS NULL OR fp.Month = @Month)
    AND (@FeeTypeId IS NULL OR fp.FeeTypeId = @FeeTypeId)
  ORDER BY fp.CreatedAt DESC
  OFFSET ((@PageNo - 1) * @PageSize) ROWS
  FETCH NEXT @PageSize ROWS ONLY;
END
GO

PRINT 'Done. FeePayments soft-delete columns + SPs created.';
GO
