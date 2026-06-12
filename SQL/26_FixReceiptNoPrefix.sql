-- ============================================================
-- Change receipt prefix from RCP to RKC  |  26_FixReceiptNoPrefix.sql
-- ============================================================
USE SchoolManagementDB;
GO

-- Update existing receipts (optional - rename old ones too)
UPDATE FeePayments
SET ReceiptNo = REPLACE(ReceiptNo, 'RCP-', 'RKC-')
WHERE ReceiptNo LIKE 'RCP-%';
GO

-- Recreate sp_SaveFeePayment with RKC prefix
IF OBJECT_ID('sp_SaveFeePayment','P') IS NOT NULL DROP PROC sp_SaveFeePayment;
GO
CREATE PROCEDURE sp_SaveFeePayment
  @StudentId      INT,
  @FeeTypeId      INT = NULL,
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
      'RKC-' + CAST(YEAR(GETDATE()) AS NVARCHAR) + '-'
      + RIGHT('0000' + CAST((SELECT ISNULL(MAX(PaymentId),0)+1 FROM FeePayments) AS NVARCHAR), 4);

  INSERT INTO FeePayments
      (ReceiptNo, StudentId, FeeTypeId, Amount, Discount, LateFine, NetAmount,
       PaymentDate, PaymentMode, TransactionRef, AcademicYearId, Month, Remarks, CollectedBy)
  VALUES
      (@RNo, @StudentId,
       NULLIF(@FeeTypeId, 0),
       @Amount, @Discount, @LateFine,
       @Amount - @Discount + @LateFine,
       @PaymentDate, @PaymentMode, @TransactionRef,
       @AcademicYearId, @Month, @Remarks, @CollectedBy);

  SET @NewPaymentId = SCOPE_IDENTITY();
END
GO

PRINT '✅ Receipt prefix changed to RKC, existing receipts updated.';
GO
