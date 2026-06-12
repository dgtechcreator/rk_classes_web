-- ============================================================
-- Fix FK error on FeePayments.FeeTypeId  |  25_FixFeePaymentFKError.sql
-- The Pay page collects a lump-sum (not per fee type), so
-- FeeTypeId must be nullable in FeePayments.
-- ============================================================
USE SchoolManagementDB;
GO

-- ── Step 1: Drop the FK constraint ──────────────────────────
-- Find and drop the FK on FeePayments.FeeTypeId
DECLARE @fk NVARCHAR(200);
SELECT @fk = fk.name
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.parent_object_id = OBJECT_ID('FeePayments') AND c.name = 'FeeTypeId';

IF @fk IS NOT NULL
    EXEC('ALTER TABLE FeePayments DROP CONSTRAINT ' + @fk);
PRINT 'FK dropped (or not found).';

-- ── Step 2: Make FeePayments.FeeTypeId nullable ──────────────
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('FeePayments') AND name = 'FeeTypeId' AND is_nullable = 0
)
BEGIN
    ALTER TABLE FeePayments ALTER COLUMN FeeTypeId INT NULL;
    PRINT 'FeePayments.FeeTypeId set to nullable.';
END
ELSE
    PRINT 'FeePayments.FeeTypeId already nullable.';

-- ── Step 3: Re-add FK as nullable-safe ──────────────────────
-- SQL Server FK on nullable column: NULLs are allowed, non-NULLs must match
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    WHERE fk.parent_object_id = OBJECT_ID('FeePayments')
      AND fk.name = 'FK_FeePayments_FeeTypes'
)
BEGIN
    ALTER TABLE FeePayments
    ADD CONSTRAINT FK_FeePayments_FeeTypes
    FOREIGN KEY (FeeTypeId) REFERENCES FeeTypes(FeeTypeId);
    PRINT 'FK re-added as nullable-safe.';
END
GO

-- ── Step 4: Update sp_SaveFeePayment to accept NULL FeeTypeId ─
IF OBJECT_ID('sp_SaveFeePayment','P') IS NOT NULL DROP PROC sp_SaveFeePayment;
GO
CREATE PROCEDURE sp_SaveFeePayment
  @StudentId      INT,
  @FeeTypeId      INT = NULL,          -- NULL = lump-sum / general payment
  @Amount         DECIMAL(10,2),
  @Discount       DECIMAL(10,2) = 0,
  @LateFine       DECIMAL(10,2) = 0,
  @PaymentDate    DATE,
  @PaymentMode    NVARCHAR(30)  = 'Cash',
  @TransactionRef NVARCHAR(100) = NULL,
  @AcademicYearId INT           = NULL,
  @Month          NVARCHAR(20)  = NULL,
  @Remarks        NVARCHAR(300) = NULL,
  @CollectedBy    INT           = NULL,
  @NewPaymentId   INT OUTPUT
AS
BEGIN
  SET NOCOUNT ON;

  DECLARE @RNo NVARCHAR(30) =
      'RCP-' + CAST(YEAR(GETDATE()) AS NVARCHAR) + '-'
      + RIGHT('0000' + CAST((SELECT ISNULL(MAX(PaymentId),0)+1 FROM FeePayments) AS NVARCHAR), 4);

  INSERT INTO FeePayments
      (ReceiptNo, StudentId, FeeTypeId, Amount, Discount, LateFine, NetAmount,
       PaymentDate, PaymentMode, TransactionRef, AcademicYearId, Month, Remarks, CollectedBy)
  VALUES
      (@RNo, @StudentId,
       NULLIF(@FeeTypeId, 0),          -- treat 0 same as NULL (safety)
       @Amount, @Discount, @LateFine,
       @Amount - @Discount + @LateFine,
       @PaymentDate, @PaymentMode, @TransactionRef,
       @AcademicYearId, @Month, @Remarks, @CollectedBy);

  SET @NewPaymentId = SCOPE_IDENTITY();
END
GO

PRINT '✅ FeePayments.FeeTypeId is now nullable, SP updated.';
PRINT '   Run this once in SSMS, then restart the app.';
GO
