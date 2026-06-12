-- ============================================================
-- 32: Fix DeleteFeePayment — reverse StudentFees.PaidAmount
--     and fix RestoreFeePayment to add it back
-- ============================================================

-- ── sp_DeleteFeePayment (updated) ─────────────────────────
IF OBJECT_ID('sp_DeleteFeePayment','P') IS NOT NULL DROP PROC sp_DeleteFeePayment;
GO
CREATE PROCEDURE sp_DeleteFeePayment
  @PaymentId INT,
  @DeletedBy INT = NULL
AS BEGIN
  SET NOCOUNT ON;

  -- Soft-delete the payment record
  UPDATE FeePayments
  SET IsDeleted = 1,
      DeletedAt = GETDATE(),
      DeletedBy = @DeletedBy
  WHERE PaymentId = @PaymentId;

  -- Reverse the PaidAmount on the matching StudentFee record
  -- Match by StudentId + FeeTypeId + Month + AcademicYearId (best-effort)
  DECLARE @StudentId     INT,
          @FeeTypeId     INT,
          @Month         NVARCHAR(20),
          @AcYearId      INT,
          @NetAmount     DECIMAL(10,2);

  SELECT @StudentId = StudentId,
         @FeeTypeId = FeeTypeId,
         @Month     = Month,
         @AcYearId  = AcademicYearId,
         @NetAmount = NetAmount
  FROM   FeePayments
  WHERE  PaymentId = @PaymentId;

  -- Deduct from StudentFees.PaidAmount and recalculate status
  UPDATE StudentFees
  SET PaidAmount = CASE WHEN PaidAmount - @NetAmount < 0 THEN 0
                        ELSE PaidAmount - @NetAmount END,
      Status     = CASE
                     WHEN (CASE WHEN PaidAmount-@NetAmount<0 THEN 0 ELSE PaidAmount-@NetAmount END) <= 0
                       THEN 'Pending'
                     WHEN (CASE WHEN PaidAmount-@NetAmount<0 THEN 0 ELSE PaidAmount-@NetAmount END) >= (Amount - Discount + LateFine)
                       THEN 'Paid'
                     ELSE 'Partial'
                   END
  WHERE StudentId     = @StudentId
    AND FeeTypeId     = @FeeTypeId
    AND (Month        = @Month OR (@Month IS NULL AND Month IS NULL))
    AND (AcademicYearId = @AcYearId OR @AcYearId IS NULL);

END
GO

-- ── sp_RestoreFeePayment (updated) ────────────────────────
IF OBJECT_ID('sp_RestoreFeePayment','P') IS NOT NULL DROP PROC sp_RestoreFeePayment;
GO
CREATE PROCEDURE sp_RestoreFeePayment
  @PaymentId INT
AS BEGIN
  SET NOCOUNT ON;

  -- Un-delete the payment
  UPDATE FeePayments
  SET IsDeleted = 0,
      DeletedAt = NULL,
      DeletedBy = NULL
  WHERE PaymentId = @PaymentId;

  -- Add back the NetAmount to StudentFees.PaidAmount
  DECLARE @StudentId INT, @FeeTypeId INT, @Month NVARCHAR(20),
          @AcYearId INT, @NetAmount DECIMAL(10,2);

  SELECT @StudentId = StudentId,
         @FeeTypeId = FeeTypeId,
         @Month     = Month,
         @AcYearId  = AcademicYearId,
         @NetAmount = NetAmount
  FROM   FeePayments
  WHERE  PaymentId = @PaymentId;

  UPDATE StudentFees
  SET PaidAmount = PaidAmount + @NetAmount,
      Status     = CASE
                     WHEN (PaidAmount + @NetAmount) >= (Amount - Discount + LateFine) THEN 'Paid'
                     WHEN (PaidAmount + @NetAmount) > 0 THEN 'Partial'
                     ELSE 'Pending'
                   END
  WHERE StudentId     = @StudentId
    AND FeeTypeId     = @FeeTypeId
    AND (Month        = @Month OR (@Month IS NULL AND Month IS NULL))
    AND (AcademicYearId = @AcYearId OR @AcYearId IS NULL);

END
GO

PRINT 'Done: sp_DeleteFeePayment and sp_RestoreFeePayment updated with PaidAmount reversal.';
GO
